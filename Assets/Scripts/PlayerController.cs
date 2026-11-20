using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace SintLucas.JorritExamples.PlayerController
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        private float moveSpeed = 5;
        [SerializeField] float walkSpeed = 5;
        [SerializeField] float runSpeed = 10f;
        [SerializeField] float gravity = -30f;
        [SerializeField] float jumpHeight = 3.5f;
        [SerializeField] float rotationSpeed = 10f;
        [SerializeField] bool canJump = true;
        [Tooltip("It is recommended to create a player layer and excluding it from the ground layer.")]
        [SerializeField] LayerMask groundMask;
        [SerializeField] private float maxStepDown = 0.6f;

        [Header("Camera rotation")]
        [SerializeField] Vector2 sensitivity = new Vector2(750f, 750f);
        [SerializeField] Transform camHolder;
        [Range(0f, 180f)]
        [SerializeField] float xClamp = 85f;

        private CharacterController characterController;
        Vector3 verticalVelocity = Vector3.zero;
        bool isGrounded;
        float xRot = 0f;

        // local fields
        private Vector2 _prevInput = Vector2.zero;
        [SerializeField] private Animator animator;
        [SerializeField] private float inputThreshold = 0.1f;
        [SerializeField] private float animationSpeedMultiplier = 1.5f;

        private Vector3 currentHorizontalVelocity = Vector3.zero;

        public bool canMove = true;
        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            // Use CharacterController's grounded state (reliable with Move).
            isGrounded = characterController.isGrounded;

            if (canMove)
            {
                Move();

                if (isGrounded && Input.GetKeyDown(KeyCode.Space) && canJump)
                {
                    OnJumpPressed();
                }

                moveSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

                UpdateCameraRot();
            }

            ApplyGravity();
            PlayerAnimation();
        }

        private void Move()
        {
            // read input
            Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));

            // prefer main camera; fallback to camHolder or player forward
            Transform cam = Camera.main != null ? Camera.main.transform : (camHolder != null ? camHolder : transform);

            Vector3 camForward = cam.forward;
            Vector3 camRight = cam.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 wishDir = (camRight * input.x + camForward * input.z).normalized;

            if (isGrounded)
            {
                // Ground movement — full control
                currentHorizontalVelocity = wishDir * moveSpeed;

                // reset a small downward velocity to keep grounded stable
                if (verticalVelocity.y < 0f)
                    verticalVelocity.y = -2f;
            }
            else
            {
                // Some air control (blend towards desired direction)
                Vector3 target = wishDir * moveSpeed;
                currentHorizontalVelocity = Vector3.Lerp(currentHorizontalVelocity, target, 0.1f);
            }

            // apply horizontal movement
            Vector3 horizontalMove = currentHorizontalVelocity * Time.deltaTime;

            bool hasGroundSupport = isGrounded ||
                (verticalVelocity.y <= 0f && HasGroundAtFeet());

            if (hasGroundSupport && horizontalMove.sqrMagnitude > 0.0001f &&
                !HasGroundAhead(horizontalMove))
            {
                horizontalMove = Vector3.zero;
            }

            characterController.Move(horizontalMove);

            // rotate player to movement direction
            if (wishDir.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(wishDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        private void PlayerAnimation()
        {
            // in Update()
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            float inputMag = input.magnitude;

            // started moving this frame?
            bool startedMoving = _prevInput.magnitude <= inputThreshold && inputMag > inputThreshold;
            // stopped moving this frame?
            bool stoppedMoving = _prevInput.magnitude > inputThreshold && inputMag <= inputThreshold;

            // feed animator
            animator.SetBool("isMoving", inputMag > inputThreshold);

            // feed speed for blend tree (normalize as needed)
            animator.SetFloat("moveSpeed", inputMag * (moveSpeed / runSpeed) * animationSpeedMultiplier);
            //Debug.Log(inputMag * (moveSpeed / runSpeed));

            _prevInput = input;
        }
        private void ApplyGravity()
        {
            verticalVelocity.y += gravity * Time.deltaTime;
            characterController.Move(verticalVelocity * Time.deltaTime);
        }

        private void OnJumpPressed()
        {
            verticalVelocity.y = Mathf.Sqrt(-2f * jumpHeight * gravity);
        }

        private void UpdateCameraRot()
        {
            if (camHolder == null)
                return;

            float mouseX = Input.GetAxis("Mouse X") * sensitivity.x * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity.y * Time.deltaTime;

            // Rotate camera holder horizontally
            camHolder.Rotate(Vector3.up * mouseX);

            // Clamp vertical look (pitch)
            xRot -= mouseY;
            xRot = Mathf.Clamp(xRot, -xClamp, xClamp);
            camHolder.localRotation = Quaternion.Euler(xRot, camHolder.localEulerAngles.y, 0f);
        }

        private bool HasGroundAhead(Vector3 movement)
        {
            Vector3 bottom = transform.position
                + characterController.center
                - Vector3.up * (characterController.height * 0.5f);

            Vector3 probePosition = bottom
                + movement
                + movement.normalized * (characterController.radius * 0.75f);

            Vector3 rayOrigin = probePosition
                + Vector3.up * (characterController.stepOffset + 0.1f);

            float rayDistance = characterController.stepOffset + maxStepDown + 0.2f;

            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                    rayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            float drop = bottom.y - hit.point.y;
            bool withinDropLimit = drop <= maxStepDown;
            bool walkableSlope = Vector3.Angle(hit.normal, Vector3.up)
                <= characterController.slopeLimit;

            return withinDropLimit && walkableSlope;
        }

        private bool HasGroundAtFeet()
        {
            Vector3 bottom = transform.position
                + characterController.center
                - Vector3.up * (characterController.height * 0.5f);

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            float offset = characterController.radius * 0.6f;

            return HasWalkableGroundAt(bottom)
                || HasWalkableGroundAt(bottom + forward * offset)
                || HasWalkableGroundAt(bottom - forward * offset)
                || HasWalkableGroundAt(bottom + right * offset)
                || HasWalkableGroundAt(bottom - right * offset);
        }

        private bool HasWalkableGroundAt(Vector3 point)
        {
            Vector3 origin = point + Vector3.up * (characterController.stepOffset + 0.1f);
            float distance = characterController.stepOffset + 0.25f;

            return Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                       distance, groundMask, QueryTriggerInteraction.Ignore)
                && Vector3.Angle(hit.normal, Vector3.up) <= characterController.slopeLimit;
        }

        public bool IsGrounded() => isGrounded;

        public bool IsRunning() => moveSpeed == runSpeed;

        public bool IsMoving()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            return new Vector2(h, v).magnitude > 0.1f;
        }
    }
}