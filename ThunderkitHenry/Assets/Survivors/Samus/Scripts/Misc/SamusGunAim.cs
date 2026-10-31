using RoR2;
using UnityEngine;

namespace SamusMod.Misc
{
    // The model has no AimAnimator, so the arm cannon only follows the run and idle animations. After the animator has posed
    // the arm, swing it so the barrel points at what the player is aiming at.
    public class SamusGunAim : MonoBehaviour
    {
        public Transform pivot;
        public Transform muzzle;
        public InputBankTest inputBank;
        public CharacterBody body;
        public float maxAngle = 80f;
        public float aimDistance = 60f;
        public float responsiveness = 15f;

        private Quaternion smoothed = Quaternion.identity;

        private void LateUpdate()
        {
            bool alive = !body || !body.healthComponent || body.healthComponent.alive;
            if (!pivot || !muzzle || !inputBank || !alive || SkillStates.BaseStates.BaseSamus.morphBall)
            {
                smoothed = Quaternion.identity;
                return;
            }

            Vector3 target = inputBank.GetAimRay().GetPoint(aimDistance);
            Vector3 current = muzzle.position - pivot.position;
            Vector3 desired = target - pivot.position;
            if (current.sqrMagnitude < 1e-4f || desired.sqrMagnitude < 1e-4f)
            {
                return;
            }

            Quaternion delta = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(current, desired), maxAngle);
            smoothed = Quaternion.Slerp(smoothed, delta, 1f - Mathf.Exp(-responsiveness * Time.deltaTime));
            pivot.rotation = smoothed * pivot.rotation;
        }
    }
}
