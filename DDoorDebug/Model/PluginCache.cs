using Cinemachine;
using System.Collections.Generic;
using UnityEngine;

namespace DDoorDebug.Model
{
    public class PluginCache
    {
        public Camera mainCam;
        public CinemachineBrain cineBrain;
        public CinemachineVirtualCamera virtCam;
        public LineRenderer lineRenderer;
        public Mesh sphereMesh;
        public Mesh boxMesh;
        //-
        public readonly RaycastHit[] hitsCache = new RaycastHit[20];
        public readonly Vector3[] lineCache = new Vector3[2];
        public readonly Vector3[] boxCachePoints = new Vector3[24];
        public readonly List<BoxCollider> boxData = new List<BoxCollider>(100);
        public readonly List<MeshCollider> meshData = new List<MeshCollider>(30);
        public readonly List<CapsuleData> capsuleData = new List<CapsuleData>(60);
        public readonly List<SphereCollider> sphereData = new List<SphereCollider>(15);
        public readonly MaterialPropertyBlock matProps = new MaterialPropertyBlock();

        public class CapsuleData
        {
            public Mesh mesh;
            public CapsuleCollider collider;
        }

        public void ClearColliderCache()
        {
            boxData.Clear();
            meshData.Clear();
            capsuleData.Clear();
            sphereData.Clear();
        }
    }
}
