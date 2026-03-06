using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[ExecuteAlways]
public class EasyIK : MonoBehaviour
{
    [Header("IK Properties")]
    [Range(2, 10)] public int numberOfJoints = 2;
    public Transform ikTarget;
    [Range(1, 50)] public int iterations = 10;
    [Range(0.001f, 0.1f)] public float tolerance = 0.001f;

    [Header("End Effector")]
    public bool matchTargetRotation = true;

    [Header("Twist Distribution")]
    public bool enableTwist = true;
    public TwistAxis twistAxis = TwistAxis.X;
    [Range(0f, 1f)] public float twistFalloff = 0.5f;
    public AnimationCurve twistCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("Manual weights per joint (auto-generated if empty)")]
    public float[] twistWeights;

    [Header("Pole Target (3 joint chain)")]
    public Transform poleTarget;

    [Header("Debug")]
    public bool debugJoints = true;
    public bool localRotationAxis;
    [Range(0.01f, 1f)] public float gizmoSize = 0.05f;
    public bool poleDirection;
    public bool poleRotationAxis;
    public bool showTwistAxes;

    public enum TwistAxis { X, Y, Z, NegX, NegY, NegZ }

    // Calibration data
    [SerializeField, HideInInspector] private float[] _boneLengths;
    [SerializeField, HideInInspector] private float _chainLength;
    [SerializeField, HideInInspector] private Vector3[] _localAxes;
    [SerializeField, HideInInspector] private Quaternion[] _restLocalRotations;
    [SerializeField, HideInInspector] private Quaternion _endEffectorLocalRot;
    [SerializeField, HideInInspector] private Vector3 _calibratedTwistAxis;
    [SerializeField, HideInInspector] private bool _isCalibrated;

    // Runtime
    private Transform[] _joints;
    private NativeArray<float3> _positions;
    private NativeArray<float> _nativeBoneLengths;
    private Quaternion[] _solvedRotations;
    private bool _initialized;

    public bool IsCalibrated => _isCalibrated;
    public Transform[] Joints => _joints;

    private Vector3 TwistAxisVector => twistAxis switch
    {
        TwistAxis.X => Vector3.right,
        TwistAxis.Y => Vector3.up,
        TwistAxis.Z => Vector3.forward,
        TwistAxis.NegX => Vector3.left,
        TwistAxis.NegY => Vector3.down,
        TwistAxis.NegZ => Vector3.back,
        _ => Vector3.right
    };

    private void OnEnable()
    {
        BuildJointArray();
        InitializeNative();
    }

    private void OnDisable() => DisposeNative();
    private void OnDestroy() => DisposeNative();

    private void BuildJointArray()
    {
        _joints = new Transform[numberOfJoints];
        _solvedRotations = new Quaternion[numberOfJoints];
        var current = transform;

        for (int i = 0; i < numberOfJoints && current != null; i++)
        {
            _joints[i] = current;
            _solvedRotations[i] = Quaternion.identity;
            current = current.childCount > 0 ? current.GetChild(0) : null;
        }
    }

    private void InitializeNative()
    {
        DisposeNative();
        if (!_isCalibrated) return;

        _positions = new NativeArray<float3>(numberOfJoints, Allocator.Persistent);
        _nativeBoneLengths = new NativeArray<float>(numberOfJoints, Allocator.Persistent);

        for (int i = 0; i < _boneLengths.Length && i < numberOfJoints; i++)
            _nativeBoneLengths[i] = _boneLengths[i];

        _initialized = true;
    }

    private void DisposeNative()
    {
        if (_positions.IsCreated) _positions.Dispose();
        if (_nativeBoneLengths.IsCreated) _nativeBoneLengths.Dispose();
        _initialized = false;
    }

    public void Calibrate()
    {
        BuildJointArray();

        _boneLengths = new float[numberOfJoints];
        _localAxes = new Vector3[numberOfJoints];
        _restLocalRotations = new Quaternion[numberOfJoints];
        _chainLength = 0;
        _calibratedTwistAxis = TwistAxisVector;

        for (int i = 0; i < numberOfJoints; i++)
        {
            if (_joints[i] == null) return;

            _restLocalRotations[i] = _joints[i].localRotation;

            if (i < numberOfJoints - 1 && _joints[i + 1] != null)
            {
                Vector3 worldDir = (_joints[i + 1].position - _joints[i].position).normalized;
                _localAxes[i] = _joints[i].InverseTransformDirection(worldDir);
                _boneLengths[i] = Vector3.Distance(_joints[i].position, _joints[i + 1].position);
                _chainLength += _boneLengths[i];
            }
        }

        if (ikTarget != null && _joints[numberOfJoints - 1] != null)
        {
            _endEffectorLocalRot = Quaternion.Inverse(ikTarget.rotation) * _joints[numberOfJoints - 1].rotation;
        }

        // Auto-generate twist weights if not set
        if (twistWeights == null || twistWeights.Length != numberOfJoints)
        {
            GenerateTwistWeights();
        }

        _isCalibrated = true;
        InitializeNative();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public void GenerateTwistWeights()
    {
        twistWeights = new float[numberOfJoints];

        for (int i = 0; i < numberOfJoints; i++)
        {
            // Normalized position in chain (0 = root, 1 = tip)
            float t = numberOfJoints > 1 ? (float)i / (numberOfJoints - 1) : 0f;
            
            // Apply curve and falloff
            float curveValue = twistCurve.Evaluate(t);
            twistWeights[i] = curveValue * twistFalloff;
        }

        // End effector always gets full twist if matching rotation
        twistWeights[numberOfJoints - 1] = 1f;
    }

    public void ResetToRestPose()
    {
        if (!_isCalibrated || _joints == null) return;

        for (int i = 0; i < numberOfJoints && i < _restLocalRotations.Length; i++)
        {
            if (_joints[i] != null)
                _joints[i].localRotation = _restLocalRotations[i];
        }
    }

    private void LateUpdate()
    {
        if (!_isCalibrated || !_initialized || ikTarget == null) return;
        if (_joints == null || _joints[0] == null) return;

        SolveIK();
    }

    private void SolveIK()
    {
        for (int i = 0; i < numberOfJoints; i++)
        {
            if (_joints[i] == null) return;
            _positions[i] = _joints[i].position;
        }

        float3 target = ikTarget.position;
        float3 root = _positions[0];
        bool usePole = poleTarget != null && numberOfJoints == 3;

        var job = new FABRIKJob
        {
            Positions = _positions,
            BoneLengths = _nativeBoneLengths,
            TargetPosition = target,
            RootPosition = root,
            ChainLength = _chainLength,
            Tolerance = tolerance,
            Iterations = iterations,
            UsePole = usePole,
            PolePosition = usePole ? (float3)poleTarget.position : float3.zero
        };

        job.Schedule().Complete();

        ApplyIK();
    }

    private void ApplyIK()
    {
        // First pass: calculate base rotations (pointing bones at targets)
        for (int i = 0; i < numberOfJoints - 1; i++)
        {
            if (_joints[i] == null || _joints[i + 1] == null) continue;

            Vector3 targetDir = ((Vector3)_positions[i + 1] - (Vector3)_positions[i]).normalized;
            Vector3 currentDir = _joints[i].TransformDirection(_localAxes[i]);

            if (targetDir.sqrMagnitude > 0.0001f && currentDir.sqrMagnitude > 0.0001f)
            {
                Quaternion deltaRot = Quaternion.FromToRotation(currentDir, targetDir);
                _joints[i].rotation = deltaRot * _joints[i].rotation;
            }

            _solvedRotations[i] = _joints[i].rotation;
        }

        // Calculate end effector rotation
        Quaternion endEffectorTargetRot = matchTargetRotation
            ? ikTarget.rotation * _endEffectorLocalRot
            : _joints[numberOfJoints - 1].rotation;

        _joints[numberOfJoints - 1].rotation = endEffectorTargetRot;
        _solvedRotations[numberOfJoints - 1] = endEffectorTargetRot;

        // Second pass: distribute twist
        if (enableTwist && numberOfJoints > 2)
        {
            ApplyTwistDistribution();
        }
    }

    private void ApplyTwistDistribution()
    {
        if (twistWeights == null || twistWeights.Length != numberOfJoints) return;

        // Get the twist axis in world space from the end effector
        Vector3 worldTwistAxis = _joints[numberOfJoints - 1].TransformDirection(_calibratedTwistAxis);

        // Calculate the twist angle of the end effector relative to what it would be without twist
        // We compare the current up vector to what it "should" be
        
        // Get the "neutral" rotation (just pointing at target, no twist)
        int lastBoneIdx = numberOfJoints - 2;
        if (lastBoneIdx < 0) return;

        Vector3 boneDir = ((Vector3)_positions[numberOfJoints - 1] - (Vector3)_positions[lastBoneIdx]).normalized;
        
        // The actual end effector rotation
        Quaternion actualRot = _joints[numberOfJoints - 1].rotation;
        
        // Neutral rotation would be the bone direction with no twist
        Quaternion neutralRot = _solvedRotations[lastBoneIdx];
        
        // Extract twist from the difference
        Quaternion twistDelta = ExtractTwist(Quaternion.Inverse(neutralRot) * actualRot, _calibratedTwistAxis);
        
        // Get twist angle
        twistDelta.ToAngleAxis(out float twistAngle, out Vector3 axis);
        
        // Make sure we're rotating around the right axis direction
        if (Vector3.Dot(axis, _calibratedTwistAxis) < 0)
            twistAngle = -twistAngle;

        // Apply weighted twist to each joint (except last one which already has full rotation)
        for (int i = 0; i < numberOfJoints - 1; i++)
        {
            if (_joints[i] == null) continue;

            float weight = twistWeights[i];
            if (weight <= 0.001f) continue;

            // Calculate twist for this joint
            float jointTwist = twistAngle * weight;
            
            // Get the twist axis in this joint's local space
            Vector3 localTwistAxis = _calibratedTwistAxis;
            
            // Apply twist rotation
            Quaternion twistRot = Quaternion.AngleAxis(jointTwist, localTwistAxis);
            _joints[i].localRotation = _joints[i].localRotation * twistRot;
        }
    }

    private Quaternion ExtractTwist(Quaternion rotation, Vector3 twistAxis)
    {
        // Project rotation onto twist axis
        Vector3 rotationAxis = new Vector3(rotation.x, rotation.y, rotation.z);
        float dot = Vector3.Dot(rotationAxis, twistAxis);
        Vector3 projected = twistAxis * dot;

        Quaternion twist = new Quaternion(projected.x, projected.y, projected.z, rotation.w);
        
        float magnitude = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
        
        if (magnitude < 0.0001f)
            return Quaternion.identity;

        return new Quaternion(twist.x / magnitude, twist.y / magnitude, twist.z / magnitude, twist.w / magnitude);
    }

    [BurstCompile(FloatPrecision.High, FloatMode.Strict)]
    private struct FABRIKJob : IJob
    {
        public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float> BoneLengths;

        public float3 TargetPosition;
        public float3 RootPosition;
        public float ChainLength;
        public float Tolerance;
        public int Iterations;
        public bool UsePole;
        public float3 PolePosition;

        public void Execute()
        {
            int count = Positions.Length;
            float dist = math.distance(RootPosition, TargetPosition);

            if (dist >= ChainLength - 0.0001f)
            {
                float3 dir = math.normalizesafe(TargetPosition - RootPosition);
                for (int i = 1; i < count; i++)
                    Positions[i] = Positions[i - 1] + dir * BoneLengths[i - 1];
                return;
            }

            for (int iter = 0; iter < Iterations; iter++)
            {
                float endDist = math.distance(Positions[count - 1], TargetPosition);
                if (endDist <= Tolerance) break;

                Positions[count - 1] = TargetPosition;
                for (int i = count - 2; i >= 0; i--)
                {
                    float3 dir = math.normalizesafe(Positions[i] - Positions[i + 1]);
                    Positions[i] = Positions[i + 1] + dir * BoneLengths[i];
                }

                Positions[0] = RootPosition;
                for (int i = 1; i < count; i++)
                {
                    float3 dir = math.normalizesafe(Positions[i] - Positions[i - 1]);
                    Positions[i] = Positions[i - 1] + dir * BoneLengths[i - 1];
                }
            }

            if (UsePole && count == 3)
                ApplyPole();
        }

        private void ApplyPole()
        {
            float3 a = Positions[0];
            float3 b = Positions[1];
            float3 c = Positions[2];

            float3 limbDir = math.normalizesafe(c - a);
            float limbLen = math.distance(a, c);

            if (limbLen < 0.0001f) return;

            float t = math.dot(b - a, limbDir);
            float3 projectedMid = a + limbDir * t;

            float3 midOffset = b - projectedMid;
            float midOffsetLen = math.length(midOffset);

            if (midOffsetLen < 0.0001f) return;

            float3 poleOffset = PolePosition - projectedMid;
            poleOffset = poleOffset - limbDir * math.dot(poleOffset, limbDir);
            float poleOffsetLen = math.length(poleOffset);

            if (poleOffsetLen < 0.0001f) return;

            float3 newMidOffset = (poleOffset / poleOffsetLen) * midOffsetLen;
            Positions[1] = projectedMid + newMidOffset;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_isCalibrated && _joints == null)
            BuildJointArray();
    }
#endif
}