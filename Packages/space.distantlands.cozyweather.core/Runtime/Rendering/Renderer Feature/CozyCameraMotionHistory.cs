//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DistantLands.Cozy
{
    public class CozyCameraMotionHistory : CameraHistoryItem
    {
        private Vector3 m_PrevPosition;
        private Quaternion m_PrevRotation;
        private Matrix4x4 m_PrevViewMatrix;
        private Matrix4x4 m_PrevViewProjMatrix;
        private readonly Matrix4x4[] m_PrevViewProjMatrices = new Matrix4x4[2];
        private bool m_HasPreviousFrame;

        private Vector3 m_PendingPosition;
        private Quaternion m_PendingRotation;
        private Matrix4x4 m_PendingViewMatrix;
        private Matrix4x4 m_PendingViewProjMatrix;
        private readonly Matrix4x4[] m_PendingViewProjMatrices = new Matrix4x4[2];

        private int m_LastFrameNumber = -1;

        public override void Reset()
        {
            m_HasPreviousFrame = false;
            m_LastFrameNumber = -1;
            System.Array.Clear(m_PrevViewProjMatrices, 0, m_PrevViewProjMatrices.Length);
            System.Array.Clear(m_PendingViewProjMatrices, 0, m_PendingViewProjMatrices.Length);
        }
        public bool TryGetPreviousFrame(out Vector3 position, out Quaternion rotation, out Matrix4x4 viewMatrix, out Matrix4x4 viewProjMatrix)
        {
            AdvanceToCurrentFrame();

            position = m_PrevPosition;
            rotation = m_PrevRotation;
            viewMatrix = m_PrevViewMatrix;
            viewProjMatrix = m_PrevViewProjMatrix;
            return m_HasPreviousFrame;
        }

        public void RecordCurrentFrame(Camera camera, Matrix4x4 viewMatrix, Matrix4x4 viewProjMatrix)
        {
            AdvanceToCurrentFrame();

            m_PendingPosition = camera.transform.position;
            m_PendingRotation = camera.transform.rotation;
            m_PendingViewMatrix = viewMatrix;
            m_PendingViewProjMatrix = viewProjMatrix;
            m_PendingViewProjMatrices[0] = viewProjMatrix;
            m_PendingViewProjMatrices[1] = viewProjMatrix;
        }

        public bool TryGetPreviousViewProjMatrices(out Matrix4x4[] viewProjMatrices)
        {
            AdvanceToCurrentFrame();
            viewProjMatrices = m_PrevViewProjMatrices;
            return m_HasPreviousFrame;
        }

        public void RecordCurrentFrame(Camera camera, Matrix4x4 viewMatrix, Matrix4x4[] viewProjMatrices)
        {
            AdvanceToCurrentFrame();

            m_PendingPosition = camera.transform.position;
            m_PendingRotation = camera.transform.rotation;
            m_PendingViewMatrix = viewMatrix;
            m_PendingViewProjMatrix = viewProjMatrices[0];
            System.Array.Copy(viewProjMatrices, m_PendingViewProjMatrices, Mathf.Min(viewProjMatrices.Length, m_PendingViewProjMatrices.Length));
        }

        private void AdvanceToCurrentFrame()
        {
            if (m_LastFrameNumber == Time.frameCount)
                return;

            if (m_LastFrameNumber >= 0)
            {
                m_PrevPosition = m_PendingPosition;
                m_PrevRotation = m_PendingRotation;
                m_PrevViewMatrix = m_PendingViewMatrix;
                m_PrevViewProjMatrix = m_PendingViewProjMatrix;
                System.Array.Copy(m_PendingViewProjMatrices, m_PrevViewProjMatrices, m_PendingViewProjMatrices.Length);
                m_HasPreviousFrame = true;
            }

            m_LastFrameNumber = Time.frameCount;
        }
    }
}