using UnityEngine;

namespace Ia.Core.Motion
{
    [System.Serializable]
    public struct IaSpringFloat
    {
        public float stiffness;
        public float damping;

        public float value;
        public float velocity;

        public float target;

        public void Reset(float newValue)
        {
            value = newValue;
            target = newValue;
            velocity = 0f;
        }

        public void Update(float deltaTime)
        {
            float x = value - target;
            float accel = -stiffness * x - damping * velocity;

            velocity += accel * deltaTime;
            value += velocity * deltaTime;
        }

        public void AddImpulse(float impulse)
        {
            velocity += impulse;
        }
    }

    [System.Serializable]
    public struct IaSpringVector3
    {
        public float stiffness;
        public float damping;

        public Vector3 value;
        public Vector3 velocity;
        public Vector3 target;

        public void Reset(Vector3 newValue)
        {
            value = newValue;
            target = newValue;
            velocity = Vector3.zero;
        }

        public void Update(float deltaTime)
        {
            Vector3 x = value - target;
            Vector3 accel = -stiffness * x - damping * velocity;

            velocity += accel * deltaTime;
            value += velocity * deltaTime;
        }

        public void AddImpulse(Vector3 impulse)
        {
            velocity += impulse;
        }
    }
}