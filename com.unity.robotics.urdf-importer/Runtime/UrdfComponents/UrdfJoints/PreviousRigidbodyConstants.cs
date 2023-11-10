using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public class PreviousRigidbodyConstants : MonoBehaviour
    {

        [SerializeField] public float mass;
        [SerializeField] public float drag;
        [SerializeField] public float angularDrag;
        [SerializeField] public Vector3 centerOfMass;
        [SerializeField] public UrdfLinkDescription.Inertial.InertiaCalculationType inertiaCalculationType;
        [SerializeField] public Vector3 inertiaTensor;
        [SerializeField] public Quaternion inertiaTensorRotation;
        

        public void SetValues(UrdfLinkDescription link)
        {
            this.mass = (float) link.inertial.mass;
            //TODO - Find the drag & angularDrag...
            //previousRigidbodyConstants.drag = link.inertial.;
            //previousRigidbodyConstants.angularDrag = rigidbody.angularDrag;
            if (link.inertial.origin == null)
            {
                RuntimeUrdf.AddImportWarning($"Missing link.inertial.origin for link with name {link.name}, assuming {this.centerOfMass}");
            }
            else
            {
                this.centerOfMass = link.inertial.origin.Xyz.ToVector3().Ros2Unity();
            }

            inertiaCalculationType = link.inertial.inertia.inertiaCalculationType;
            Matrix3x3 inertia = UrdfInertial.ToUnityMatrix3x3(link.inertial.inertia);
            inertiaTensor = inertia.PxDiagonalize(out inertiaTensorRotation);
        }
        
        public void SetValues(Rigidbody from)
        {
            this.inertiaCalculationType = default;
            this.mass = from.mass;
            this.drag = from.drag;
            this.angularDrag = from.angularDrag;
            this.centerOfMass = from.centerOfMass;
            this.inertiaTensor = from.inertiaTensor;
            this.inertiaTensorRotation = from.inertiaTensorRotation;
        }

    }
}