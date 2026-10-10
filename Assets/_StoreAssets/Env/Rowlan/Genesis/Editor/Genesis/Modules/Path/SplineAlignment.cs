using JBooth.MicroVerseCore;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

#if __MICROVERSE_SPLINES__
using UnityEngine.Splines;
#endif

namespace Rowlan.Genesis
{
#if __MICROVERSE_SPLINES__
    public class SplineAlignment
    {
        private SplineContainer[] selectedSplineContainers;

        private Editor editor;

        public SplineAlignment( Editor editor)
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
            foreach (SplineContainer splineContainer in selectedSplineContainers)
            {
                splineContainer.transform.gameObject.SetActive(false);
            }

        }

        private void EnableSplines()
        {
            foreach (SplineContainer splineContainer in selectedSplineContainers)
            {
                splineContainer.transform.gameObject.SetActive(true);
            }

        }

        private MicroVerse GetMicroVerse()
        {
            return MicroVerse.instance;
        }

        private void AlignSplines()
        {
            MicroVerse microVerse = GetMicroVerse();

            foreach (SplineContainer splineContainer in selectedSplineContainers)
            {
                microVerse.AlignSplines(splineContainer);

                EditorUtility.SetDirty(splineContainer);
            }
        }

        public void AlignAllSplines()
        {
            selectedSplineContainers = GetMicroVerse().GetComponentsInChildren<SplineContainer>();

            // toggle only active spline containers
            selectedSplineContainers = selectedSplineContainers.Where(x => x.isActiveAndEnabled == true && x.transform.gameObject.activeInHierarchy == true).ToArray();

            AlignContainers();
        }

        public void AlignSpline( SplineContainer splineContainer)
        {

            selectedSplineContainers = new SplineContainer[] { splineContainer };

            // toggle only active spline containers
            selectedSplineContainers = selectedSplineContainers.Where(x => x.isActiveAndEnabled == true && x.transform.gameObject.activeInHierarchy == true).ToArray();

            AlignContainers();
        }

        private void AlignContainers()
        {
            try
            {
                DisableSplines();

                // recreate terrain without splines, no async
                GetMicroVerse().Modify(true, true);

                AlignSplines();
            }
            catch( Exception ex)
            {
                // catch the error during alignment and do nothing else
                // the other code has to be executed for cleanup
                Debug.LogError( "Spline alignment error: " + ex);
            }
            // in any case of eg an error re-enable the splines (in 2.4.0 there's one in the spline cache itself) 
            finally
            {
                EnableSplines();
            }

            // recreate terrain with splines, no async
            GetMicroVerse().Modify(true, true);

            CenterMainHandle();

            MicroVerseChanged();
        }

        private void CenterMainHandle()
        {
            foreach (SplineContainer splineContainer in selectedSplineContainers)
            {
                SplineUtils.CenterMainHandle(splineContainer);
            }
        }
    }
#endif
}