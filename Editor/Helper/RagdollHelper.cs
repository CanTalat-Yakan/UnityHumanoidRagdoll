#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace UnityEssentials
{
    public static class RagdolHelper
    {
        /// <summary>
		/// If you rotate collider, the collider rotates via an additional
		/// node that have the same name + this text.
		/// </summary>
		public const string ColliderRotatorNodeSufix = "_ColliderRotator";

        public static Vector3 Abs(Vector3 vector) =>
            new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));

        public static Vector3 GetXYZDirectionVector(Vector3 node) =>
            GetXYZDirectionIndex(node) switch
            {
                0 => Vector3.right,
                1 => Vector3.up,
                2 => Vector3.forward,
                _ => default
            };

        public static int GetXYZDirectionIndex(Vector3 node)
        {
            // Get the most appropriate direction in terms of PhysX (0,1,2 directions)

            float x = Mathf.Abs(node.x);
            float y = Mathf.Abs(node.y);
            float z = Mathf.Abs(node.z);

            // X is the biggest
            if (x > y & x > z)
                return 0;

            // Y is the biggest
            if (y > x & y > z)
                return 1;

            // Z is the biggest
            return 2;
        }

        public static Transform GetLongestTransform(Transform limb)
        {
            float longestFloat = -1;
            Transform longestTransform = null;

            // Find the farest object that attached to 'limb'
            foreach (Transform t in limb.GetComponentsInChildren<Transform>())
            {
                float length = (limb.position - t.position).sqrMagnitude;
                if (length > longestFloat)
                {
                    longestFloat = length;
                    longestTransform = t;
                }
            }

            return longestTransform;
        }

        /// <summary>
        /// Get rotation of collider object
        /// </summary>
        public static Quaternion GetRotatorRotation(Transform boneTransform)
        {
            Collider collider = GetCollider(boneTransform);
            return collider.transform.rotation;
        }

        /// <summary>
        /// Get position of collider center
        /// </summary>
        public static Vector3 GetRotatorPosition(Transform boneTransform)
        {
            Collider collider = GetCollider(boneTransform);
            CapsuleCollider cCollider = collider as CapsuleCollider;
            BoxCollider bCollider = collider as BoxCollider;
            SphereCollider sCollider = collider as SphereCollider;
            MeshCollider mCollider = collider as MeshCollider;

            Vector3 colliderCenter;
            if (cCollider != null) colliderCenter = cCollider.center;
            else if (bCollider != null) colliderCenter = bCollider.center;
            else if (sCollider != null) colliderCenter = sCollider.center;
            else if (mCollider != null) colliderCenter = mCollider.sharedMesh.bounds.center;
            else
                colliderCenter = Vector3.zero;

            var colliderTransform = collider.transform;
            return colliderTransform.TransformPoint(colliderCenter);
        }

        /// <summary>
        /// Rotate collider without rotating "transform" object.
        /// </summary>
        public static void RotateCollider(Transform transform, Quaternion rotate)
        {
            Vector3 prevPosition = GetColliderPosition(transform);

            Undo.RecordObject(transform, "Rotate collider");
            transform.rotation = rotate;

            SetColliderPosition(transform, prevPosition);
        }

        /// <summary>
        /// Get colliders' center in world space
        /// </summary>
        public static Vector3 GetColliderPosition(Transform transform)
        {
            Collider collider = GetCollider(transform);
            CapsuleCollider cCollider = collider as CapsuleCollider;
            BoxCollider bCollider = collider as BoxCollider;
            SphereCollider sCollider = collider as SphereCollider;

            Vector3 center;
            if (cCollider != null) center = cCollider.center;
            else if (bCollider != null) center = bCollider.center;
            else if (sCollider != null) center = sCollider.center;
            else
                throw new InvalidOperationException("Unsupported Collider type: " + collider.GetType().FullName);

            return collider.transform.TransformPoint(center);
        }

        /// <summary>
        /// Set colliders' center in world space
        /// </summary>
        public static void SetColliderPosition(Transform transform, Vector3 position)
        {
            Collider collider = GetCollider(transform);
            Undo.RecordObject(collider, "Set collider position");

            CapsuleCollider cCollider = collider as CapsuleCollider;
            BoxCollider bCollider = collider as BoxCollider;
            SphereCollider sCollider = collider as SphereCollider;

            Vector3 center = collider.transform.InverseTransformPoint(position);
            if (cCollider != null) cCollider.center = center;
            else if (bCollider != null) bCollider.center = center;
            else if (sCollider != null) sCollider.center = center;
            else
                throw new InvalidOperationException("Unsupported Collider type: " + collider.GetType().FullName);
        }

        /// <summary>
        /// Get object a collider attached to. 
        /// </summary>
        public static Collider GetCollider(Transform transform)
        {
            Collider collider = transform.GetComponent<Collider>();
            if (collider == null)
            {
                var rotatorName = transform.name + ColliderRotatorNodeSufix;
                var rotatorTransform = transform.Find(rotatorName);
                if (rotatorTransform != null)
                    collider = rotatorTransform.GetComponent<Collider>();
            }

            if (collider == null)
                throw new ArgumentException("transform '" + transform.name + "' does not contain collider");

            return collider;
        }

        /// <summary>
        /// Gets object a collider attached to.
        /// Collider must have separate GameObject to allow a collider to rotate via it.
        /// So if that GameObject do not exists, creates it.
        /// </summary>
        public static Transform GetRotatorTransform(Transform boneTransform)
        {
            var colliderRotatorName = boneTransform.name + ColliderRotatorNodeSufix;

            // find rotator node
            var rotatorTransform = boneTransform.Find(colliderRotatorName);
            if (rotatorTransform != null)
                return rotatorTransform;

            // if rotator node was not found, create it
            Collider collider = boneTransform.GetComponent<Collider>();
            if (collider == null)
                throw new ArgumentException("Bone '" + boneTransform.name + "' does not have collider attached to it or ColliderRotatorNode");

            GameObject colliderRotator = new GameObject(colliderRotatorName);
            Undo.RegisterCreatedObjectUndo(colliderRotator, "Create Rotator");
            rotatorTransform = colliderRotator.transform;

            ReattachCollider(boneTransform.gameObject, colliderRotator);
            Undo.SetTransformParent(rotatorTransform, boneTransform, "Set collider parrent");
            rotatorTransform.localPosition = Vector3.zero;
            rotatorTransform.localRotation = Quaternion.identity;
            rotatorTransform.localScale = Vector3.one;

            return colliderRotator.transform;
        }

        /// <summary>
        /// Duplicate collidr from "from" to "to" and delete it from "from"
        /// </summary>
        static void ReattachCollider(GameObject from, GameObject to)
        {
            var oldCollider = from.GetComponent<Collider>();
            CapsuleCollider cCollider = oldCollider as CapsuleCollider;
            BoxCollider bCollider = oldCollider as BoxCollider;
            Collider newCollider;
            if (cCollider != null)
            {
                CapsuleCollider newCapsuleCollider = Undo.AddComponent<CapsuleCollider>(to);
                newCollider = newCapsuleCollider;
                newCapsuleCollider.direction = cCollider.direction;
                newCapsuleCollider.radius = cCollider.radius;
                newCapsuleCollider.height = cCollider.height;
                newCapsuleCollider.center = cCollider.center;
            }
            else if (bCollider != null)
            {
                BoxCollider newBoxCollider = Undo.AddComponent<BoxCollider>(to);
                newCollider = newBoxCollider;
                newBoxCollider.size = bCollider.size;
                newBoxCollider.center = bCollider.center;
            }
            else
                throw new NotSupportedException("Collider type '" + oldCollider + "' does not supported to reattach.");

            newCollider.isTrigger = oldCollider.isTrigger;
            Undo.DestroyObjectImmediate(oldCollider);
        }
    }
}
#endif