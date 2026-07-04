using UnityEngine;

namespace OutGame
{
    /// <summary>
    /// Forces a World Space UI Canvas or object to continuously face the active camera.
    /// </summary>
    public class OutBillboard : MonoBehaviour
    {
        #region Inspector Fields
        [Header("Billboard Settings")]
        [SerializeField] private EOutBillboardMode _billboardMode = EOutBillboardMode.CameraAlignment;

        [Tooltip("Assign the main camera. If left null, it will find Camera.main on Awake.")]
        [SerializeField] private Camera _mainCamera;

        [Tooltip("Flip this if your UI canvas appears backwards.")]
        [SerializeField] private bool _invertFace = false;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.current;
                if (_mainCamera == null) _mainCamera = GameObject.FindWithTag("MainCamera").GetComponent<Camera>();
            }
        }

        private void LateUpdate()
        {
            if (_mainCamera == null) return;

            switch (_billboardMode)
            {
                case EOutBillboardMode.CameraAlignment:
                    // UI matches the exact rotation of the camera plane
                    transform.rotation = _mainCamera.transform.rotation;
                    if (_invertFace)
                    {
                        transform.Rotate(0, 180, 0);
                    }
                    break;

                case EOutBillboardMode.LookAtPosition:
                    // UI swivels to look directly at the camera lens
                    Vector3 directionToCamera = _mainCamera.transform.position - transform.position;

                    if (_invertFace)
                    {
                        directionToCamera = -directionToCamera;
                    }

                    transform.rotation = Quaternion.LookRotation(directionToCamera);
                    break;
            }
        }
        #endregion
    }
}