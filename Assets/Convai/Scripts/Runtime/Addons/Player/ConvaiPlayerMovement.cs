using Convai.Scripts.Runtime.Core;
using Convai.Scripts.Runtime.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Convai.Scripts.Runtime.Addons
{
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Convai/Player Movement")]
    public class ConvaiPlayerMovement : MonoBehaviour
    {
        [Header("Movement Parameters")] 
        [SerializeField] [Range(1, 10)] private float walkingSpeed = 3f;
        [SerializeField] [Range(1, 10)] private float runningSpeed = 8f;
        [SerializeField] [Range(1, 10)] private float jumpSpeed = 4f;

        [Header("Gravity & Grounding")] 
        [SerializeField] [Range(1, 10)] private float gravity = 9.8f;

        [Header("Camera Parameters")] 
        [SerializeField] private Camera playerCamera;
        [SerializeField] [Range(0.1f, 10f)] private float lookSpeed = 2.0f;
        [SerializeField] [Range(1, 90)] private float lookXLimit = 45.0f;
        
        [Header("Smoothing")]
        [SerializeField] [Range(0.01f, 0.5f)] private float smoothTime = 0.05f;

        private CharacterController _characterController;
        private Vector3 _moveDirection = Vector3.zero;
        
        // Smoothing variables
        private float _rotationX;
        private float _rotationY;
        private float _currentRotationX;
        private float _currentRotationY;
        private float _xRotationVelocity;
        private float _yRotationVelocity;

        public static ConvaiPlayerMovement Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            _characterController = GetComponent<CharacterController>();
            if (playerCamera == null) playerCamera = Camera.main;

            // Initial rotation state
            Vector3 euler = transform.eulerAngles;
            _rotationY = euler.y;
            _currentRotationY = euler.y;
            
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            MovePlayer();
            RotatePlayerAndCamera();
        }

        private void OnEnable()
        {
            if (ConvaiInputManager.Instance != null)
                ConvaiInputManager.Instance.jumping += Jump;
        }

        private void OnDisable()
        {
            if (ConvaiInputManager.Instance != null)
                ConvaiInputManager.Instance.jumping -= Jump;
        }

        private void MovePlayer()
        {
            Vector3 horizontalMovement = Vector3.zero;

            if (!UIUtilities.IsAnyInputFieldFocused())
            {
                Vector3 forward = transform.TransformDirection(Vector3.forward);
                Vector3 right = transform.TransformDirection(Vector3.right);

                float speed = ConvaiInputManager.Instance.isRunning ? runningSpeed : walkingSpeed;
                Vector2 moveVector = ConvaiInputManager.Instance.moveVector;

                horizontalMovement = (forward * moveVector.y + right * moveVector.x) * speed;
            }

            if (!_characterController.isGrounded)
                _moveDirection.y -= gravity * Time.deltaTime;

            _characterController.Move((_moveDirection + horizontalMovement) * Time.deltaTime);
        }

        private void Jump()
        {
            if (_characterController.isGrounded && !UIUtilities.IsAnyInputFieldFocused()) 
                _moveDirection.y = jumpSpeed;
        }

        private void RotatePlayerAndCamera()
        {
            bool isRightClickHeld = UnityEngine.InputSystem.Mouse.current.rightButton.isPressed;

            if (isRightClickHeld)
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }

                // Get raw input
                Vector2 lookInput = ConvaiInputManager.Instance.lookVector;

                // Accumulate target rotations
                _rotationX -= lookInput.y * lookSpeed;
                _rotationY += lookInput.x * lookSpeed;

                _rotationX = Mathf.Clamp(_rotationX, -lookXLimit, lookXLimit);
            }
            else
            {
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }

            // ALWAYS apply smoothing, even if the button was just released.
            // This "catches" the jerk and smooths it out over a few frames.
            _currentRotationX = Mathf.SmoothDampAngle(_currentRotationX, _rotationX, ref _xRotationVelocity, smoothTime);
            _currentRotationY = Mathf.SmoothDampAngle(_currentRotationY, _rotationY, ref _yRotationVelocity, smoothTime);

            playerCamera.transform.localRotation = Quaternion.Euler(_currentRotationX, 0, 0);
            transform.rotation = Quaternion.Euler(0, _currentRotationY, 0);
        }
    }
}