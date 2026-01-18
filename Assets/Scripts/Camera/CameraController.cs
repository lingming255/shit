using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Third-person camera controller that follows and rotates with the local network player.
/// The player always faces the camera direction.
/// </summary>
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }
    
    [Header("Target Settings")]
    [Tooltip("Distance from the player")]
    public float distance = 10f;
    
    [Tooltip("Height offset from player pivot")]
    public Vector3 targetOffset = new Vector3(0, 1.5f, 0);

    [Header("Rotation Settings")]
    [Tooltip("Mouse sensitivity")]
    public float rotationSpeed = 3f;
    
    [Tooltip("Minimum vertical angle (looking up)")]
    public float minVerticalAngle = -20f;
    
    [Tooltip("Maximum vertical angle (looking down)")]
    public float maxVerticalAngle = 60f;

    [Header("Smoothing")]
    [Tooltip("How smoothly the camera follows the player")]
    public float smoothSpeed = 10f;

    [Header("First Person Settings")]
    [Tooltip("Offset for first person view (in front of player)")]
    public Vector3 firstPersonOffset = new Vector3(0, 0f, 1.5f);

    // Internal state
    private Transform _target;
    private float _currentYaw;
    private float _currentPitch = 20f;
    private Vector3 _currentVelocity;
    private bool _isFirstPerson = false;
    private float _currentDistance;

    /// <summary>
    /// The current horizontal direction the camera is facing (for movement)
    /// </summary>
    public float CameraYaw => _currentYaw;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Initialize rotation from current camera orientation
        Vector3 angles = transform.eulerAngles;
        _currentYaw = angles.y;
        _currentPitch = angles.x;
        _currentDistance = distance;
    }

    void LateUpdate()
    {
        // Try to find target if not set
        if (_target == null)
        {
            FindLocalPlayer();
            if (_target == null) return;
        }

        HandleViewToggle();
        HandleRotationInput();
        RotatePlayer();
        UpdateCameraPosition();
    }

    private void FindLocalPlayer()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient)
        {
            return;
        }
        
        var localClient = NetworkManager.Singleton.LocalClient;
        if (localClient == null || localClient.PlayerObject == null)
        {
            return;
        }
        
        _target = localClient.PlayerObject.transform;
        // Initialize yaw from player's current rotation
        _currentYaw = _target.eulerAngles.y;
        Debug.Log($"[CameraController] Found local player: {_target.name}");
    }

    /// <summary>
    /// Toggle between first and third person view with R key
    /// </summary>
    private void HandleViewToggle()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            _isFirstPerson = !_isFirstPerson;
            Debug.Log($"[CameraController] Switched to {(_isFirstPerson ? "First Person" : "Third Person")} view");
        }
    }

    private void HandleRotationInput()
    {
        if (Mouse.current == null) return;
        
        // Right mouse button to rotate camera
        bool isRotating = Mouse.current.rightButton.isPressed;

        if (isRotating)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Get mouse delta
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            float mouseX = mouseDelta.x * rotationSpeed * 0.1f;
            float mouseY = mouseDelta.y * rotationSpeed * 0.1f;

            // Apply rotation (positive X = turn right)
            _currentYaw += mouseX;
            _currentPitch -= mouseY;

            // Clamp vertical angle
            _currentPitch = Mathf.Clamp(_currentPitch, minVerticalAngle, maxVerticalAngle);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>
    /// Rotate the player to face the camera direction (horizontal only) with smooth interpolation
    /// </summary>
    private void RotatePlayer()
    {
        if (_target == null) return;
        
        // Target rotation based on camera yaw
        Quaternion targetRotation = Quaternion.Euler(0, _currentYaw, 0);
        
        // Smoothly interpolate player rotation for silky smooth turning
        _target.rotation = Quaternion.Slerp(
            _target.rotation, 
            targetRotation, 
            Time.deltaTime * smoothSpeed * 2f
        );
    }

    private void UpdateCameraPosition()
    {
        Vector3 desiredPosition;
        
        if (_isFirstPerson)
        {
            // FIRST PERSON: Camera EXACTLY in front of player, NO Y offset
            // Position = player position + forward direction * 2 units
            Vector3 frontPosition = _target.position + _target.forward * 2f;
            
            // Set position directly - no smoothing, no offsets
            transform.position = frontPosition;
            
            // Look forward with pitch for up/down view
            transform.rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0);
        }
        else
        {
            // Third person: camera behind player
            Vector3 targetPosition = _target.position + targetOffset;
            Quaternion rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0);
            Vector3 offset = rotation * new Vector3(0, 0, -distance);
            desiredPosition = targetPosition + offset;
            
            // Smoothly move camera
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref _currentVelocity,
                1f / smoothSpeed
            );
            
            // Look at target
            transform.LookAt(targetPosition);
        }
    }

    /// <summary>
    /// Convert input direction to world direction based on camera facing
    /// </summary>
    public Vector3 GetMovementDirection(float inputX, float inputZ)
    {
        // Get camera forward and right (ignore vertical)
        Vector3 forward = Quaternion.Euler(0, _currentYaw, 0) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0, _currentYaw, 0) * Vector3.right;
        
        // Combine input with camera directions
        return (forward * inputZ + right * inputX).normalized;
    }
}
