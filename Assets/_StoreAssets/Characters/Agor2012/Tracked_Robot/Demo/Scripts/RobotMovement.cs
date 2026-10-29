using UnityEngine;
using UnityEngine.InputSystem;

namespace Agor.Robot
{
    public class RobotMovement : MonoBehaviour
    {
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference attack1Action;
        [SerializeField] private InputActionReference attack2Action;
        [SerializeField] private InputActionReference antennaAction;
        [SerializeField] private InputActionReference torsoLeftAction;
        [SerializeField] private InputActionReference torsoRightAction;

        [SerializeField] private Transform torso;
        [SerializeField] private Animator animator;

        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float turnSpeed = 90f;
        [SerializeField] private float torsoTurnSpeed = 90f;

        private float torsoAngle;
        private Quaternion torsoBaseRotation;
        private bool torsoBaseRotationCaptured;

        private void OnEnable()
        {
            moveAction.action.Enable();
            attack1Action.action.Enable();
            attack2Action.action.Enable();
            antennaAction.action.Enable();
            torsoLeftAction.action.Enable();
            torsoRightAction.action.Enable();
        }

        private void OnDisable()
        {
            moveAction.action.Disable();
            attack1Action.action.Disable();
            attack2Action.action.Disable();
            antennaAction.action.Disable();
            torsoLeftAction.action.Disable();
            torsoRightAction.action.Disable();
        }

        private void Update()
        {
            Vector2 input = moveAction.action.ReadValue<Vector2>();

            // ЛКМ — атака левой рукой
            if (attack1Action.action.WasPressedThisFrame())
            {
                animator.SetTrigger("Attack1");
            }

            // ПКМ — атака правой рукой
            if (attack2Action.action.WasPressedThisFrame())
            {
                animator.SetTrigger("Attack2");
            }

            // R — открыть / закрыть антенну
            if (antennaAction.action.WasPressedThisFrame())
            {
                bool isOpen = animator.GetBool("AntennaOpen");
                animator.SetBool("AntennaOpen", !isOpen);
            }

            // Q / E — изменение угла торса
            float torsoInput = 0f;

            if (torsoLeftAction.action.IsPressed())
            {
                torsoInput -= 1f;
            }

            if (torsoRightAction.action.IsPressed())
            {
                torsoInput += 1f;
            }

            torsoAngle += torsoInput * torsoTurnSpeed * Time.deltaTime;

            // A / D — поворот всего робота
            transform.Rotate(
                0f,
                input.x * turnSpeed * Time.deltaTime,
                0f
            );

            // W / S — движение вперёд / назад
            transform.position +=
                transform.forward *
                input.y *
                moveSpeed *
                Time.deltaTime;

            // Анимация гусениц
            animator.SetFloat("TrackSpeed", input.y);
        }

        private void LateUpdate()
        {
            // Первый раз запоминаем исходный поворот Torso
            if (!torsoBaseRotationCaptured)
            {
                torsoBaseRotation = torso.localRotation;
                torsoBaseRotationCaptured = true;
            }

            // Устанавливаем абсолютный накопленный угол,
            // а не прибавляем его снова каждый кадр
            torso.localRotation =
                torsoBaseRotation *
                Quaternion.Euler(0f, torsoAngle, 0f);
        }
    }
}