using UnityEngine;

namespace Sixty.World
{
    public class SpinBob : MonoBehaviour
    {
        [SerializeField] private float spinSpeed = 90f;
        [SerializeField] private float bobHeight = 0.25f;
        [SerializeField] private float bobFrequency = 2f;

        private Vector3 basePosition;

        private void Start()
        {
            basePosition = transform.position;
        }

        private void Update()
        {
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

            float bobOffset = Mathf.Sin(Time.time * bobFrequency) * bobHeight;
            transform.position = new Vector3(basePosition.x, basePosition.y + bobOffset, basePosition.z);
        }
    }
}
