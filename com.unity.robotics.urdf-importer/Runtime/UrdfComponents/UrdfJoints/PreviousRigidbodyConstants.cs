using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public class PreviousRigidbodyConstants : MonoBehaviour
    {

        [SerializeField] public float mass;
        [SerializeField] public float drag;
        [SerializeField] public float angularDrag;
        [SerializeField] public Vector3 centerOfMass;

    }
}