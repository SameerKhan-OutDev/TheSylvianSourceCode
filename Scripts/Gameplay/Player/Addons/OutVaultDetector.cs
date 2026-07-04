using UnityEngine;
using MxM;
using MxMGameplay;

namespace OutGame
{
    /// <summary>
    /// Analyzes the environment to trigger appropriate Motion Matching vault or drop events.
    /// Handles manual climbing and automatic ledge step-offs.
    /// </summary>
    public class OutVaultDetector : MonoBehaviour
    {
        #region Inspector Fields
        [SerializeField] private VaultDefinition[] m_vaultDefinitions = null;
        [SerializeField] private VaultDetectionConfig[] m_vaultConfigurations = null;
        [SerializeField] private float m_minStepUpDepth = 1f;
        [SerializeField] private LayerMask m_layerMask = new LayerMask();
        [SerializeField] private float m_minAdvance = 0.1f;
        [SerializeField] private float m_advanceSmoothing = 10f;
        [SerializeField] public float m_maxApproachAngle = 60f;
        #endregion

        #region Private Variables
        private MxMAnimator m_mxmAnimator;
        private MxMRootMotionApplicator m_rootMotionApplicator;
        private GenericControllerWrapper m_controllerWrapper;
        private MxMTrajectoryGenerator m_trajectoryGenerator;

        private int m_vaultAnalysisIterations;
        private VaultDetectionConfig m_curConfig;

        private float m_minVaultRise;
        private float m_maxVaultRise;
        private float m_minVaultDepth;
        private float m_maxVaultDepth;
        private float m_minVaultDrop;
        private float m_maxVaultDrop;

        private bool m_isVaulting;

        public float Advance { get; set; }
        public float DesiredAdvance { get; set; }
        #endregion

        #region Unity Lifecycle
        public void Awake()
        {
            if (m_vaultConfigurations == null || m_vaultConfigurations.Length == 0 || m_vaultDefinitions == null || m_vaultDefinitions.Length == 0)
            {
                Debug.LogError("OutVaultDetector: Missing vault configurations or definitions.");
                Destroy(this);
                return;
            }

            m_mxmAnimator = GetComponentInChildren<MxMAnimator>();
            m_trajectoryGenerator = GetComponentInChildren<MxMTrajectoryGenerator>();
            m_rootMotionApplicator = GetComponentInChildren<MxMRootMotionApplicator>();
            m_controllerWrapper = GetComponentInChildren<GenericControllerWrapper>();

            m_minVaultRise = float.MaxValue;
            m_maxVaultRise = float.MinValue;
            m_minVaultDepth = float.MaxValue;
            m_maxVaultDepth = float.MinValue;
            m_minVaultDrop = float.MaxValue;
            m_maxVaultDrop = float.MinValue;

            foreach (VaultDefinition vd in m_vaultDefinitions)
            {
                switch (vd.VaultType)
                {
                    case EVaultType.StepUp:
                        if (vd.MinRise < m_minVaultRise) m_minVaultRise = vd.MinRise;
                        if (vd.MaxRise > m_maxVaultRise) m_maxVaultRise = vd.MaxRise;
                        if (vd.MinDepth < m_minVaultDepth) m_minVaultDepth = vd.MinDepth;
                        break;
                    case EVaultType.StepOver:
                        if (vd.MinRise < m_minVaultRise) m_minVaultRise = vd.MinRise;
                        if (vd.MaxRise > m_maxVaultRise) m_maxVaultRise = vd.MaxRise;
                        if (vd.MinDepth < m_minVaultDepth) m_minVaultDepth = vd.MinDepth;
                        if (vd.MaxDepth > m_maxVaultDepth) m_maxVaultDepth = vd.MaxDepth;
                        break;
                    case EVaultType.StepOff:
                        if (vd.MinDepth < m_minVaultDepth) m_minVaultDepth = vd.MinDepth;
                        if (vd.MinDrop < m_minVaultDrop) m_minVaultDrop = vd.MinDrop;
                        if (vd.MaxDrop > m_maxVaultDrop) m_maxVaultDrop = vd.MaxDrop;
                        break;
                }
            }

            m_curConfig = m_vaultConfigurations[0];
            DesiredAdvance = Advance = 0f;
            m_vaultAnalysisIterations = (int)(m_maxVaultDepth / m_curConfig.ShapeAnalysisSpacing) + 1;
        }

        public void OnEnable()
        {
            m_isVaulting = false;
        }

        public void Update()
        {
            if (m_isVaulting)
            {
                HandleCurrentVault();
                return;
            }

            if (!CanVault())
                return;

            Vector3 charPos = transform.position;
            Vector3 charForward = transform.forward;
            float approachAngle = 0f;

            // FIX 2: Lowered the probe start to catch shorter objects
            Vector3 probeStart = new Vector3(charPos.x, charPos.y + (m_minVaultRise * 0.8f), charPos.z);
            Ray forwardRay = new Ray(probeStart, charForward);
            RaycastHit forwardRayHit;

            if (Physics.SphereCast(forwardRay, m_curConfig.DetectProbeRadius, out forwardRayHit, Advance, m_layerMask, QueryTriggerInteraction.Ignore))
            {
                if (forwardRayHit.distance < Advance)
                    Advance = forwardRayHit.distance;

                Vector3 obstacleOrient = Vector3.ProjectOnPlane(forwardRayHit.normal, Vector3.up) * -1f;
                approachAngle = Vector3.SignedAngle(transform.forward, obstacleOrient, Vector3.up);

                if (Mathf.Abs(approachAngle) > m_maxApproachAngle)
                    return;
            }

            probeStart = transform.TransformPoint(new Vector3(0f, 0f, Advance));
            probeStart.y += m_maxVaultRise;

            Ray probeRay = new Ray(probeStart, Vector3.down);
            RaycastHit probeHit;

            if (Physics.SphereCast(probeRay, m_curConfig.DetectProbeRadius, out probeHit, m_maxVaultRise + m_maxVaultDrop, m_layerMask, QueryTriggerInteraction.Ignore))
            {
                if (probeHit.distance < Mathf.Epsilon)
                    return;

                if (probeHit.distance < (m_maxVaultRise - m_minVaultRise))
                {
                    float ledgeHeight = probeHit.point.y - charPos.y;

                    // 1. Tall Wall / Door Passthrough Check: 
                    // Cast forward slightly above the detected ledge. If it hits something, the wall continues upward.
                    Vector3 clearanceProbeStart = charPos + (Vector3.up * (ledgeHeight + 0.15f));
                    if (Physics.Raycast(clearanceProbeStart, charForward, out _, Advance + 0.2f, m_layerMask, QueryTriggerInteraction.Ignore))
                    {
                        return; // It's a continuous wall or door, not a vaultable ledge.
                    }

                    // 2. Ceiling / Head-Banger Check:
                    // Ensure the player has clear upward space to reach the ledge without clipping the ceiling.
                    float headStartHeight = m_controllerWrapper.Height;
                    float neededHeight = ledgeHeight + 0.5f; // Needs a 0.5m buffer for the body/head arc

                    if (neededHeight > headStartHeight)
                    {
                        float upDistance = neededHeight - headStartHeight;
                        if (Physics.Raycast(charPos + (Vector3.up * headStartHeight), Vector3.up, out _, upDistance, m_layerMask, QueryTriggerInteraction.Ignore))
                        {
                            return; // A ceiling is blocking the vault trajectory.
                        }
                    }

                    if (!CheckCharacterHeightFit(probeHit.point, charForward))
                        return;

                    VaultShapeAnalysis(in probeHit, out VaultableProfile vaultable);

                    if (vaultable.VaultType == EVaultType.Invalid)
                        return;

                    if (vaultable.VaultType == EVaultType.StepUp && vaultable.Depth < m_minStepUpDepth)
                        return;

                    VaultDefinition vaultDef = ComputeBestVault(ref vaultable);
                    if (vaultDef == null)
                        return;

                    // FIX 3: Require Vault Button ONLY for StepUp and StepOver, allowing auto-drops
                    if (vaultDef.VaultType == EVaultType.StepUp || vaultDef.VaultType == EVaultType.StepOver)
                    {
                        if (!OutInputManager.Instance.InputActions.Player.Vault.IsPressed())
                            return;
                    }

                    float facingAngle = transform.rotation.eulerAngles.y;
                    if (vaultDef.LineUpWithObstacle)
                    {
                        facingAngle += approachAngle;
                    }

                    vaultDef.EventDefinition.ClearContacts();

                    switch (vaultDef.OffsetMethod_Contact1)
                    {
                        case EVaultContactOffsetMethod.Offset: vaultable.Contact1 += transform.TransformVector(vaultDef.Offset_Contact1); break;
                        case EVaultContactOffsetMethod.DepthProportion: vaultable.Contact1 += transform.TransformVector(vaultable.Depth * vaultDef.Offset_Contact1); break;
                    }

                    vaultDef.EventDefinition.AddEventContact(vaultable.Contact1, facingAngle);

                    if (vaultable.VaultType == EVaultType.StepOver)
                    {
                        switch (vaultDef.OffsetMethod_Contact2)
                        {
                            case EVaultContactOffsetMethod.Offset: vaultable.Contact2 += transform.TransformVector(vaultDef.Offset_Contact2); break;
                            case EVaultContactOffsetMethod.DepthProportion: vaultable.Contact2 += transform.TransformVector(vaultable.Depth * vaultDef.Offset_Contact2); break;
                        }

                        vaultDef.EventDefinition.AddEventContact(vaultable.Contact2, facingAngle);
                    }

                    m_mxmAnimator.BeginEvent(vaultDef.EventDefinition);
                    m_mxmAnimator.PostEventTrajectoryMode = EPostEventTrajectoryMode.Pause;

                    if (m_rootMotionApplicator != null) m_rootMotionApplicator.EnableGravity = false;
                    if (vaultDef.DisableCollision && m_controllerWrapper != null) m_controllerWrapper.CollisionEnabled = false;

                    m_isVaulting = true;
                }
                else
                {
                    Vector3 flatHitPoint = new Vector3(probeHit.point.x, 0f, probeHit.point.z);
                    Vector3 flatProbePoint = new Vector3(probeStart.x, 0f, probeStart.z);
                    Vector3 dir = flatProbePoint - flatHitPoint;

                    if (dir.sqrMagnitude > (m_curConfig.DetectProbeRadius * m_curConfig.DetectProbeRadius) / 4f)
                    {
                        Vector2 start2D = new Vector2(probeStart.x, probeStart.z);
                        Vector2 hit2D = new Vector2(probeHit.point.x, probeHit.point.z);
                        float hitOffset = Vector2.Distance(start2D, hit2D);

                        VaultOffShapeAnalysis(in probeHit, out VaultableProfile vaultable, hitOffset);

                        if (vaultable.VaultType == EVaultType.Invalid)
                            return;

                        VaultDefinition vaultDef = ComputeBestVault(ref vaultable);
                        if (vaultDef == null)
                            return;

                        // StepOff automatically triggers, no manual input check required here

                        float facingAngle = transform.rotation.eulerAngles.y;
                        vaultDef.EventDefinition.ClearContacts();

                        switch (vaultDef.OffsetMethod_Contact1)
                        {
                            case EVaultContactOffsetMethod.Offset: vaultable.Contact1 += transform.TransformVector(vaultDef.Offset_Contact1); break;
                            case EVaultContactOffsetMethod.DepthProportion: vaultable.Contact1 += transform.TransformVector(vaultable.Depth * vaultDef.Offset_Contact1); break;
                        }

                        vaultDef.EventDefinition.AddEventContact(vaultable.Contact1, facingAngle);
                        m_mxmAnimator.BeginEvent(vaultDef.EventDefinition);
                        m_mxmAnimator.PostEventTrajectoryMode = EPostEventTrajectoryMode.Pause;

                        m_isVaulting = true;
                    }
                }
            }
        }
        #endregion

        #region Vault Detection Logic
        private void HandleCurrentVault()
        {
            if (m_rootMotionApplicator.EnableGravity == m_mxmAnimator.QueryUserTags(EUserTags.UserTag1))
            {
                m_rootMotionApplicator.EnableGravity = !m_rootMotionApplicator.EnableGravity;
            }

            if (m_controllerWrapper.CollisionEnabled == m_mxmAnimator.QueryUserTags(EUserTags.UserTag2))
            {
                m_controllerWrapper.CollisionEnabled = !m_controllerWrapper.CollisionEnabled;
            }

            if (m_mxmAnimator.IsEventComplete)
            {
                m_isVaulting = false;

                if (m_rootMotionApplicator != null) m_rootMotionApplicator.EnableGravity = true;
                if (m_controllerWrapper != null) m_controllerWrapper.CollisionEnabled = true;

                Advance = 0f;
            }
        }

        /// <summary>
        /// Checks if the character is in a valid state to initiate environmental scanning.
        /// </summary>
        private bool CanVault()
        {
            if (!m_controllerWrapper.IsGrounded) return false;
            if (!m_trajectoryGenerator.HasMovementInput()) return false;

            float inputAngleDelta = Vector3.Angle(transform.forward, m_trajectoryGenerator.LinearInputVector);
            if (inputAngleDelta > 45f) return false;

            DesiredAdvance = (m_mxmAnimator.BodyVelocity * m_curConfig.DetectProbeAdvanceTime).magnitude;
            Advance = Mathf.Lerp(Advance, DesiredAdvance, 1f - Mathf.Exp(-m_advanceSmoothing));

            // FIX 1: Clamp advance to prevent sprinting from completely overshooting objects
            Advance = Mathf.Clamp(Advance, m_minAdvance, 1.5f);

            if (Advance < m_minAdvance) return false;

            return true;
        }

        private bool CheckCharacterHeightFit(Vector3 a_fromPoint, Vector3 a_forward)
        {
            float radius = m_controllerWrapper.Radius;
            Vector3 fromPosition = a_fromPoint + (a_forward * radius * 2f) + (Vector3.up * radius * 1.1f);

            Ray upRay = new Ray(fromPosition, Vector3.up);
            if (Physics.Raycast(upRay, out _, m_controllerWrapper.Height, m_layerMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return true;
        }

        private void VaultOffShapeAnalysis(in RaycastHit a_rayHit, out VaultableProfile a_vaultProfile, float hitOffset)
        {
            a_vaultProfile = new VaultableProfile();

            Vector3 lastPoint = a_rayHit.point;
            bool stepOffStart = false;
            for (int i = 1; i < m_vaultAnalysisIterations; ++i)
            {
                Vector3 start = transform.TransformPoint(Vector3.forward * (Advance + hitOffset + (float)i * m_curConfig.ShapeAnalysisSpacing));
                start.y += m_maxVaultRise;

                Ray ray = new Ray(start, Vector3.down);
                RaycastHit rayHit;

                if (Physics.Raycast(ray, out rayHit, m_maxVaultRise + m_maxVaultDrop, m_layerMask, QueryTriggerInteraction.Ignore))
                {
                    float deltaHeight = rayHit.point.y - lastPoint.y;

                    if (!stepOffStart)
                    {
                        if (deltaHeight < -m_minVaultDrop)
                        {
                            a_vaultProfile.Drop = Mathf.Abs(deltaHeight);
                            a_vaultProfile.Contact1 = rayHit.point;
                            stepOffStart = true;
                        }
                    }
                    else
                    {
                        if (deltaHeight > m_minVaultRise)
                        {
                            a_vaultProfile.Depth = a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;

                            if (a_vaultProfile.Depth > 1f)
                            {
                                a_vaultProfile.Rise = deltaHeight;
                                a_vaultProfile.VaultType = EVaultType.StepOff;
                            }
                            else
                            {
                                a_vaultProfile.VaultType = EVaultType.Invalid;
                            }

                            return;
                        }
                        else if (i == m_vaultAnalysisIterations - 1)
                        {
                            a_vaultProfile.Rise = 0f;
                            a_vaultProfile.Depth = a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;
                            a_vaultProfile.VaultType = EVaultType.StepOff;
                            return;
                        }
                    }
                }
                else
                {
                    a_vaultProfile.Depth = a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;

                    if (a_vaultProfile.Depth > 1f)
                    {
                        a_vaultProfile.Rise = 0f;
                        a_vaultProfile.VaultType = EVaultType.StepOff;
                    }
                    else
                    {
                        a_vaultProfile.VaultType = EVaultType.Invalid;
                    }

                    return;
                }

                lastPoint = rayHit.point;
            }
        }

        private void VaultShapeAnalysis(in RaycastHit a_rayHit, out VaultableProfile a_vaultProfile)
        {
            a_vaultProfile = new VaultableProfile();

            a_vaultProfile.Contact1 = a_rayHit.point;
            Vector3 charPos = transform.position;
            Vector3 lastPoint = a_rayHit.point;
            Vector3 highestPoint = lastPoint;
            Vector3 lowestPoint = charPos;

            a_vaultProfile.Rise = a_rayHit.point.y - charPos.y;

            for (int i = 1; i < m_vaultAnalysisIterations; ++i)
            {
                Vector3 start = a_rayHit.point + transform.TransformVector(Vector3.forward * (float)i * m_curConfig.ShapeAnalysisSpacing);

                start.y += charPos.y + m_maxVaultRise;
                Ray ray = new Ray(start, Vector3.down);
                RaycastHit rayHit;

                if (Physics.Raycast(ray, out rayHit, m_maxVaultRise + m_maxVaultDrop, m_layerMask, QueryTriggerInteraction.Ignore))
                {
                    if (rayHit.point.y > highestPoint.y) highestPoint = rayHit.point;
                    else if (rayHit.point.y < lowestPoint.y) lowestPoint = rayHit.point;

                    float deltaHeight = rayHit.point.y - lastPoint.y;

                    if (deltaHeight < -m_minVaultDrop)
                    {
                        a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;
                        a_vaultProfile.Drop = a_rayHit.point.y - rayHit.point.y;
                        a_vaultProfile.VaultType = EVaultType.StepOver;
                        a_vaultProfile.Contact2 = rayHit.point;
                        return;
                    }
                    else if (i == m_vaultAnalysisIterations - 1)
                    {
                        a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;
                        a_vaultProfile.Drop = 0f;
                        a_vaultProfile.VaultType = EVaultType.StepUp;
                    }
                }
                else
                {
                    a_vaultProfile.Drop = m_maxVaultDrop;
                    a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;
                    a_vaultProfile.VaultType = EVaultType.StepOverFall;
                    return;
                }

                lastPoint = rayHit.point;
            }
        }

        private VaultDefinition ComputeBestVault(ref VaultableProfile a_vaultable)
        {
            foreach (VaultDefinition vaultDef in m_vaultDefinitions)
            {
                if (vaultDef.VaultType == a_vaultable.VaultType)
                {
                    switch (vaultDef.VaultType)
                    {
                        case EVaultType.StepUp:
                            if (a_vaultable.Depth < vaultDef.MinDepth) continue;
                            if (a_vaultable.Rise < vaultDef.MinRise || a_vaultable.Rise > vaultDef.MaxRise) continue;
                            break;
                        case EVaultType.StepOver:
                            if (a_vaultable.Depth < vaultDef.MinDepth || a_vaultable.Depth > vaultDef.MaxDepth) continue;
                            if (a_vaultable.Rise < vaultDef.MinRise || a_vaultable.Rise > vaultDef.MaxRise) continue;
                            if (a_vaultable.Drop < vaultDef.MinDrop || a_vaultable.Drop > vaultDef.MaxDrop) continue;
                            break;
                        case EVaultType.StepOff:
                            if (a_vaultable.Depth < vaultDef.MinDepth) continue;
                            if (a_vaultable.Drop < vaultDef.MinDrop || a_vaultable.Drop > vaultDef.MaxDrop) continue;
                            break;
                    }

                    return vaultDef;
                }
            }
            return null;
        }
        #endregion
    }
}