using JBooth.MicroVerseCore;
using System;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

#if __MICROVERSE_SPLINES__
using UnityEngine.Splines;
#endif

namespace Rowlan.Genesis
{
#if __MICROVERSE_SPLINES__

    [Obsolete("Not used any more, MV works with async off using the Modify method", true)]
    public class SplineAlignmentAsync
    {
        public bool isAlignmentActive = false;

        private SplineContainer[] processedSplineContainers;

        private Editor editor;

        public SplineAlignmentAsync( Editor editor)
        {
            this.editor = editor;
        }


        /// <summary>
        /// Spline changed, invalidate MicroVerse and repaint the scene
        /// </summary>
        private void MicroVerseChanged()
        {
            GetMicroVerse().Invalidate();

            editor?.Repaint();
        }

        private void DisableSplines()
        {
            foreach (SplineContainer splineContainer in processedSplineContainers)
            {
                splineContainer.transform.gameObject.SetActive(false);
            }

            MicroVerseChanged();

        }

        private void EnableSplines()
        {
            foreach (SplineContainer splineContainer in processedSplineContainers)
            {
                splineContainer.transform.gameObject.SetActive(true);
            }

            MicroVerseChanged();
        }

        private MicroVerse GetMicroVerse()
        {
            return MicroVerse.instance;
        }

        private void AlignSplines()
        {
            MicroVerse microVerse = GetMicroVerse();

            foreach (SplineContainer splineContainer in processedSplineContainers)
            {
                microVerse.AlignSplines(splineContainer);

                EditorUtility.SetDirty(splineContainer);
            }
        }

        /// <summary>
        /// Disable the splines to get the original heightmaps. Align the splines. Re-enabled the splines.
        /// 
        /// Aligned needs to be done in steps. Doesn't work if you have all in 1 iteration.
        /// </summary>
        public void AlignAllSplines()
        {
            if (isAlignmentActive)
            {
                Debug.Log("Alignment already running. Aborting");
                return;
            }

            isAlignmentActive = true;

            processedSplineContainers = GetMicroVerse().GetComponentsInChildren<SplineContainer>();

            EditorCoroutineUtility.StartCoroutine(StartAlignAll(), this);
        }

        public void AlignSpline( SplineContainer splineContainer)
        {
            if (isAlignmentActive)
            {
                Debug.Log("Alignment already running. Aborting");
                return;
            }

            isAlignmentActive = true;

            processedSplineContainers = new SplineContainer[] { splineContainer };

            EditorCoroutineUtility.StartCoroutine(StartAlignAll(), this);
        }

        private IEnumerator StartAlignAll()
        {
            yield return EditorCoroutineUtility.StartCoroutine(StartDisableSpline(), this);
            yield return EditorCoroutineUtility.StartCoroutine(StartSyncHeightmap(), this);
            yield return EditorCoroutineUtility.StartCoroutine(StartAlignSpline(), this);
            yield return EditorCoroutineUtility.StartCoroutine(StartEnableSpline(), this);
            yield return EditorCoroutineUtility.StartCoroutine(StartSplineChanged(), this);
            yield return EditorCoroutineUtility.StartCoroutine(StartProcessFinished(), this);
            yield return EditorCoroutineUtility.StartCoroutine(StartCenterMainHandle(), this);

        }

        private IEnumerator StartDisableSpline()
        {
            yield return null;
            DisableSplines();
        }

        private IEnumerator StartSyncHeightmap()
        {
            yield return null;
            GetMicroVerse().SyncHeightmaps();
        }

        private IEnumerator StartAlignSpline()
        {
            yield return null;
            AlignSplines();
        }

        private IEnumerator StartEnableSpline()
        {
            yield return null;
            EnableSplines();
        }

        private IEnumerator StartSplineChanged()
        {
            yield return null;
            MicroVerseChanged();
        }

        private IEnumerator StartProcessFinished()
        {
            yield return null;
            isAlignmentActive = false;
        }

        // quick solution to make alignment work
        // TODO: find out how alignment after path change works properly
        private IEnumerator StartCenterMainHandle()
        {
            yield return null;

            foreach (SplineContainer splineContainer in processedSplineContainers)
            {
                SplineUtils.CenterMainHandle(splineContainer);
            }
        }
    }
#endif
}