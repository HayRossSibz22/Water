using UnityEngine;

public class CameraFollow : MonoBehaviour {
    public Transform target; // The target object to follow
    public Vector3 offset = new Vector3(0, 5, -10); // Offset from the target object
    public float smoothSpeed = 0.125f; // Smoothing speed for movement
    public float rotationSmoothSpeed = 0.125f; // Smoothing speed for rotation

    private void LateUpdate() {
        if (target != null) {
            // Rotate the offset with the target's rotation
            Vector3 rotatedOffset = target.rotation * offset;

            // Calculate the desired position
            Vector3 desiredPosition = target.position + rotatedOffset;

            // Smoothly interpolate to the desired position
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

            // Smoothly interpolate the rotation to look at the target
            Quaternion lookAtRotation = Quaternion.LookRotation(target.position - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookAtRotation, rotationSmoothSpeed * Time.deltaTime);
        }
    }
}