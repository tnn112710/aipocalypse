//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(LateralWeatherShift))]
    public class LateralWeatherShiftDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement field = new VisualElement();

            PropertyField changeCondition = new PropertyField();
            changeCondition.BindProperty(property.FindPropertyRelative("changeCondition"));
            field.Add(changeCondition);

            PropertyField lateralProfile = new PropertyField();
            lateralProfile.name = "lateralProfile";
            lateralProfile.label = "";
            lateralProfile.BindProperty(property.FindPropertyRelative("lateralProfile"));
            field.Add(lateralProfile);

            return field;
        }

    }

    [CustomPropertyDrawer(typeof(WeatherChangeCondition))]
    public class WeatherChangeConditionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement field = new VisualElement();


            VisualElement compareField = new VisualElement();
            compareField.style.flexDirection = FlexDirection.Row;
            field.Add(compareField);

            PropertyField conditionType = new PropertyField();
            conditionType.BindProperty(property.FindPropertyRelative("conditionType"));
            conditionType.style.width = new Length(25, LengthUnit.Percent);
            conditionType.label = "";
            compareField.Add(conditionType);

            PropertyField compare = new PropertyField();
            compare.BindProperty(property.FindPropertyRelative("compare"));
            compare.style.width = new Length(25, LengthUnit.Percent);
            compare.label = "";
            compareField.Add(compare);

            PropertyField temperature = new PropertyField();
            temperature.BindProperty(property.FindPropertyRelative("temperature"));
            temperature.style.flexGrow = 1;
            temperature.label = "";
            compareField.Add(temperature);
            PropertyField humidity = new PropertyField();
            humidity.BindProperty(property.FindPropertyRelative("humidity"));
            humidity.style.flexGrow = 1;
            humidity.label = "";
            compareField.Add(humidity);
            PropertyField pressure = new PropertyField();
            pressure.BindProperty(property.FindPropertyRelative("pressure"));
            pressure.style.flexGrow = 1;
            pressure.label = "";
            compareField.Add(pressure);
            PropertyField uvIndex = new PropertyField();
            uvIndex.BindProperty(property.FindPropertyRelative("uvIndex"));
            uvIndex.style.flexGrow = 1;
            uvIndex.label = "";
            compareField.Add(uvIndex);
            PropertyField airQuality = new PropertyField();
            airQuality.BindProperty(property.FindPropertyRelative("airQuality"));
            airQuality.style.flexGrow = 1;
            airQuality.label = "";
            compareField.Add(airQuality);
            PropertyField accumulatedSnow = new PropertyField();
            accumulatedSnow.BindProperty(property.FindPropertyRelative("accumulatedSnow"));
            accumulatedSnow.style.flexGrow = 1;
            accumulatedSnow.label = "";
            compareField.Add(accumulatedSnow);
            PropertyField accumulatedRain = new PropertyField();
            accumulatedRain.BindProperty(property.FindPropertyRelative("accumulatedRain"));
            accumulatedRain.style.flexGrow = 1;
            accumulatedRain.label = "";
            compareField.Add(accumulatedRain);
            PropertyField convectiveRainChance = new PropertyField();
            convectiveRainChance.BindProperty(property.FindPropertyRelative("convectiveRainChance"));
            convectiveRainChance.style.flexGrow = 1;
            convectiveRainChance.label = "";
            compareField.Add(convectiveRainChance);
            PropertyField stratiformRainChance = new PropertyField();
            stratiformRainChance.BindProperty(property.FindPropertyRelative("stratiformRainChance"));
            stratiformRainChance.style.flexGrow = 1;
            stratiformRainChance.label = "";
            compareField.Add(stratiformRainChance);
            PropertyField season = new PropertyField();
            season.BindProperty(property.FindPropertyRelative("season"));
            season.style.flexGrow = 1;
            season.label = "";
            compareField.Add(season);
            PropertyField time = new PropertyField();
            time.BindProperty(property.FindPropertyRelative("time"));
            time.style.flexGrow = 1;
            time.label = "";
            compareField.Add(time);
            PropertyField windSpeed = new PropertyField();
            windSpeed.BindProperty(property.FindPropertyRelative("windSpeed"));
            windSpeed.style.flexGrow = 1;
            windSpeed.label = "";
            compareField.Add(windSpeed);
            PropertyField weightedRandomChance = new PropertyField();
            weightedRandomChance.BindProperty(property.FindPropertyRelative("weightedRandomChance"));
            weightedRandomChance.style.flexGrow = 1;
            weightedRandomChance.label = "";
            compareField.Add(weightedRandomChance);

            conditionType.RegisterValueChangeCallback(evt =>
            {
                ChanceConditionType condition = (ChanceConditionType)property.FindPropertyRelative("conditionType").intValue;
                temperature.style.display = condition == ChanceConditionType.Temperature ? DisplayStyle.Flex : DisplayStyle.None;
                humidity.style.display = condition == ChanceConditionType.Humidity ? DisplayStyle.Flex : DisplayStyle.None;
                pressure.style.display = condition == ChanceConditionType.Pressure ? DisplayStyle.Flex : DisplayStyle.None;
                uvIndex.style.display = condition == ChanceConditionType.UVIndex ? DisplayStyle.Flex : DisplayStyle.None;
                airQuality.style.display = condition == ChanceConditionType.AirQuality ? DisplayStyle.Flex : DisplayStyle.None;
                accumulatedSnow.style.display = condition == ChanceConditionType.AccumulatedSnow ? DisplayStyle.Flex : DisplayStyle.None;
                accumulatedRain.style.display = condition == ChanceConditionType.AccumulatedRain ? DisplayStyle.Flex : DisplayStyle.None;
                convectiveRainChance.style.display = condition == ChanceConditionType.ConvectiveRainChance ? DisplayStyle.Flex : DisplayStyle.None;
                stratiformRainChance.style.display = condition == ChanceConditionType.StratiformRainChance ? DisplayStyle.Flex : DisplayStyle.None;
                season.style.display = condition == ChanceConditionType.Season ? DisplayStyle.Flex : DisplayStyle.None;
                time.style.display = condition == ChanceConditionType.Time ? DisplayStyle.Flex : DisplayStyle.None;
                windSpeed.style.display = condition == ChanceConditionType.WindSpeed ? DisplayStyle.Flex : DisplayStyle.None;
                weightedRandomChance.style.display = condition == ChanceConditionType.CustomCondition ? DisplayStyle.Flex : DisplayStyle.None;
            });




            return field;
        }

    }
}