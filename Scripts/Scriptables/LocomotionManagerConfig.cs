using UnityEngine;
using MxM;

namespace OutGame
{
    [CreateAssetMenu(fileName = "NewPlayerLocomotionConfig", menuName = "Player/Locomotion Config")]
    public class PlayerLocomotionConfig : ScriptableObject
    {
        [Header("Speeds & Biases")]
        public float walkSpeed = 2.0f;
        public float jogSpeed = 4.3f;
        public float sprintSpeed = 6.7f;
        public Vector2 walkBias = new Vector2(10f, 10f);
        public Vector2 jogBias = new Vector2(10f, 10f);
        public Vector2 sprintBias = new Vector2(6f, 6f);
        public float favourMultiplier = 0.6f;

        [Header("Physics & Pushing")]
        public float pushPower = 2.0f;
        public float weightLimit = 50f;

        [Header("Jump Settings & Exploits")]
        [Tooltip("Layers considered solid obstacles that should block a jump.")]
        public LayerMask jumpObstacleLayer = 1; // Default to 1 (Default layer)
        [Tooltip("How far ahead to check for walls before allowing a jump.")]
        public float jumpCheckDistance = 1.2f;
        [Tooltip("If true, bypasses the obstacle check, allowing the player to clip through walls and doors.")]
        public bool enableJumpClippingExploit = false;

        [Header("Aiming - Settings")]
        public float transitionDuration = 0.25f;
        public float aimFOV = 25f;
        public float normalFOV = 40f;
        [Tooltip("Offset applied to Orbital Follow to push the camera over the shoulder")]
        public Vector3 aimOffset = new Vector3(0.5f, 0f, 0f);
        public Vector3 normalOffset = Vector3.zero;

        [Header("Events & Tags")]
        public string runTagName = "Run";
        public string sprintTagName = "Sprint";

        [Header("MxM Profiles & Events")]
        public MxMInputProfile generalLocomotionProfile;
        public MxMInputProfile sprintLocomotionProfile;
        public MxMEventDefinition jumpDefinition;
    }
}