using DistantLands.Cozy.Eclipse;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(EclipseModule))]
    public class EclipseModuleEditor : CozyModuleEditor
    {
        private EclipseModule eclipseModule;

        VisualElement eclipsePercentageGraph;

        private void OnEnable()
        {
            eclipseModule = (EclipseModule)target;
        }

        public override VisualElement DisplayUI()
        {
            VisualElement root = new VisualElement();

            eclipsePercentageGraph = new VisualElement();
            eclipsePercentageGraph.style.height = 120;
            eclipsePercentageGraph.style.overflow = Overflow.Hidden;
            eclipsePercentageGraph.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            eclipsePercentageGraph.generateVisualContent += ctx =>
            {
                var painter = ctx.painter2D;
                float width = eclipsePercentageGraph.contentRect.width;
                float height = eclipsePercentageGraph.contentRect.height;

                Gradient gradient = new()
                {
                    colorKeys = new GradientColorKey[2]
                    {
                        new(Color.darkBlue, 1),
                        new(Color.plum, 0)
                    }
                };

                painter.fillGradient = new FillGradient()
                {
                    gradientType = GradientType.Linear,
                    addressMode = AddressMode.Clamp,
                    gradient = gradient,
                    start = new Vector2(width * 1f / 3f, 0),
                    end = new Vector2(width * 2f / 3f, height)
                };

                painter.BeginPath();
                painter.Arc(new Vector2(width / 2f, height / 2f), height / 2.5f, 0, 360);
                painter.Fill();

                painter.fillColor = new Color(0.1f, 0.1f, 0.1f);

                painter.BeginPath();
                painter.Arc(new Vector2(width / 2f - (height * (1 - eclipseModule.EclipseRatio)), height / 2f), height * eclipseModule.EclipseMoonSize / 2.5f, 0, 360);
                painter.Fill();
            };
            eclipsePercentageGraph.schedule.Execute(() =>
            {
                eclipsePercentageGraph.MarkDirtyRepaint();
            }).Every(1000);
            root.Add(eclipsePercentageGraph);

            Tooltip prediction = new Tooltip();
            prediction.style.marginTop = 4;
            prediction.schedule.Execute(() =>
            {
                if (eclipseModule.TryPredictNextSolarEclipse(out SolarEclipsePrediction nextEclipse))
                {
                    string forecast =
                        $"Next solar eclipse: {nextEclipse.Date} at {nextEclipse.Time}\n" +
                        $"In {nextEclipse.DaysUntil:F2} days | Peak strength: {nextEclipse.EclipseRatio:P0}";
                    prediction.Message = forecast;
                    prediction.tooltip = forecast;
                }
                else
                {
                    prediction.Message = "No solar eclipse predicted in the next 365 days";
                    prediction.tooltip = "No daytime solar eclipse is predicted within the next 365 in-game days using the current Time and Atmosphere settings.";
                }
            }).Every(1000);
            root.Add(prediction);

            VisualElement profileContainer = new VisualElement();
            profileContainer.AddToClassList("section-bg");
            root.Add(profileContainer);
            AddProperty(profileContainer, "profile");

            VisualElement triggerContainer = new VisualElement();
            triggerContainer.AddToClassList("section-bg");
            root.Add(triggerContainer);

            AddProperty(triggerContainer, "eclipseStyle");
            AddProperty(triggerContainer, "eclipseRatio");
            AddProperty(triggerContainer, "maximumAlignmentAngle");
            AddProperty(triggerContainer, "eclipseMoonSize");

            return root;
        }

        void UpdateEclipseGraph()
        {

        }

        private void AddProperty(VisualElement root, string propertyName)
        {
            PropertyField propertyField = new PropertyField();
            propertyField.BindProperty(serializedObject.FindProperty(propertyName));
            propertyField.RegisterValueChangeCallback(evt =>
            {
                eclipsePercentageGraph.MarkDirtyRepaint();
            });
            root.Add(propertyField);
        }
    }
}
