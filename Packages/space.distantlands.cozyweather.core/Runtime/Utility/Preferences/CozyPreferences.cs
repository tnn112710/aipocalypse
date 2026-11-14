//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy
{
    public class CozyPreferences : ScriptableObject
    {
        public static float SkyPitch => Instance.skyPitch;
        public static float SkyPitchOffset
        {
            get => Instance.skyPitchOffset;
            set => Instance.skyPitchOffset = value;
        }
        public static float SkyRotation => Instance.skyRotation + Instance.skyRotationOffset;
        public static float SkyRotationOffset
        {
            get => Instance.skyRotationOffset;
            set => Instance.skyRotationOffset = value;
        }
        public static TemperatureUnit TemperatureUnit => Instance.temperatureUnit;
        public static PressureUnit PressureUnit => Instance.pressureUnit;
        public static MeridiemTime.Unit TimeUnit => Instance.timeUnit;
        public static bool ShowSeconds => Instance.showSeconds;
        public static bool ShowMilliseconds => Instance.showMilliseconds;

        public static int MonthsPerYear => Instance.months.Length;
        public static MonthData[] Months => Instance.months;
        private static int daysPerYear = 0;
        public static int DaysPerYear
        {
            get
            {
                if (daysPerYear == 0)
                {
                    daysPerYear = Instance.months.ToList().Sum(x => x.days);
                }
                return daysPerYear;
            }
        }
        public static void ResetDaysPerYear()
        {
            daysPerYear = Instance.months.ToList().Sum(x => x.days);
        }
        public static DateFormat NumericDateFormat => Instance.dateFormat;
        public static DayOfWeek StartingWeekday => Instance.startingWeekday;

        public static bool HandleSceneLighting
        {
            get => Instance.handleSceneLighting;
            set => Instance.handleSceneLighting = value;
        }

        /// <summary>
        /// If true, the sun and moon lights will move in discrete steps instead of continuously
        /// </summary>
        public static bool QuantizeLighting
        {
            get => Instance.quantizeLighting;
            set => Instance.quantizeLighting = value;
        }
        [SerializeField]
        private bool quantizeLighting = false;

        /// <summary>
        /// Sets the minimum angle difference for the sun and moon lights to move when quantization is enabled
        /// </summary>
        public static float QuantizeLightingAngle
        {
            get => Instance.quantizeLightingAngle;
            set => Instance.quantizeLightingAngle = value;
        }
        [SerializeField]
        [Range(0, 45)]
        private float quantizeLightingAngle = 15;

        /// <summary>
        /// Sets the minimum angle that lights can be placed to prevent long shadows. If over 0, sun light will not dip below the horizon.
        /// </summary>
        [Range(-15, 90)]
        public static float MinLightAngle
        {
            get => Instance.minLightAngle;
            set => Instance.minLightAngle = value;
        }
        [SerializeField]
        private float minLightAngle = -15;

        public static float SkyFogAmount
        {
            get => Instance.skyFogAmount;
            set => Instance.skyFogAmount = value;
        }
        [SerializeField]
        [Range(0, 1)]
        private float skyFogAmount = 1;

        public static float CloudsFogAmount
        {
            get => Instance.cloudsFogAmount;
            set => Instance.cloudsFogAmount = value;
        }
        [SerializeField]
        [Range(0, 1)]
        private float cloudsFogAmount = 1;

        /// <summary>
        /// Keeps the sun and moon still in the sky in one position for higher quality lighting, static baked lighting, etc.
        /// </summary>
        public static bool StaticLighting
        {
            get => Instance.staticLighting;
            set => Instance.staticLighting = value;
        }
        [SerializeField]
        private bool staticLighting = false;

        /// <summary>
        /// Used to set the rotation of the sun and moon when static lighting is enabled
        /// </summary>
        public static Vector3 StaticLightingRotation
        {
            get => Instance.staticLightingRotation;
            set => Instance.staticLightingRotation = value;
        }
        [SerializeField]
        private Vector3 staticLightingRotation = new Vector3(0, 0, 0);

        public static bool UpdateInEditMode
        {
            get => Instance.updateInEditMode; set => Instance.updateInEditMode = value;
        }
        public static bool Fog
        {
            get => Instance.fog; set => Instance.fog = value;
        }
        public static bool FollowCamera
        {
            get => Instance.followCamera; set => Instance.followCamera = value;
        }
        public static bool Gizmos
        {
            get => Instance.gizmos; set => Instance.gizmos = value;
        }
        public static bool Tooltips => Instance.tooltips;
        public static bool Graphs => Instance.graphs;
        public static bool DontDestroyWeatherOnLoad => Instance.dontDestroyWeatherOnLoad;
        public static bool RenderFXBlocking => Instance.renderFXBlocking;
        public static float FXRenderDistance => Instance.fxRenderDistance;
        public static float FXRenderHeight => Instance.fxRenderHeight;
        public static int FXBlockingResolution => Instance.fxBlockingResolution;
        public static int FXBlockLayerMask => Instance.fxBlockingLayerMask;

        public static bool DisplayMiscEvents => Instance.displayMiscEvents;
        public static bool DisplayWeatherEvents => Instance.displayWeatherEvents;
        public static bool DisplayHolidayEvents => Instance.displayHolidayEvents;
        public static bool DisplayStatusEvents => Instance.displayStatusEvents;
        public static bool DisplayWorldEventEvents => Instance.displayWorldEventEvents;
        public static bool DisplayPlayerEvents => Instance.displayPlayerEvents;
        public static bool DisplayNPCEvents => Instance.displayNPCEvents;
        public static bool DisplayEnemyEvents => Instance.displayEnemyEvents;
        public static bool DisplayAmbientEvents => Instance.displayAmbientEvents;


        public static MeridiemBlock DawnTimes => Instance.dawnTimes;
        public static MeridiemBlock MorningTimes => Instance.morningTimes;
        public static MeridiemBlock DayTimes => Instance.dayTimes;
        public static MeridiemBlock AfternoonTimes => Instance.afternoonTimes;
        public static MeridiemBlock EveningTimes => Instance.eveningTimes;
        public static MeridiemBlock TwilightTimes => Instance.twilightTimes;
        public static MeridiemBlock NightTimes => Instance.nightTimes;

        public static CozyStyle[] Styles => Instance.styles;
        public static CozyStyle CurrentStyle => Instance.styles[Instance.styleSelection];
        public static CozyStyle SelectStyle(int id)
        {
            Instance.styleSelection = Mathf.Clamp(id, 0, Instance.styles.Length - 1);
            return CurrentStyle;
        }
        public static CozyStyle SelectStyle(string id)
        {
            if (Instance.styles.Any(x => x.name == id))
                Instance.styleSelection = Instance.styles.Select((value, index) => new { value, index = index + 1 })
                .Where(pair => pair.value.name == id)
                .Select(pair => pair.index)
                .FirstOrDefault() - 1;

            return CurrentStyle;
        }

        public static Vector3 North => -Vector3.Cross(West, Vector3.up).normalized;
        public static Vector3 West => Quaternion.AngleAxis(SkyRotation, Vector3.up) * Vector3.forward;
        public static Vector3 South => -North;
        public static Vector3 East => -West;

        public static Downsampling CloudDownsampling => Instance.cloudDownsampling;
        public static Downsampling FogDownsampling => Instance.fogDownsampling;

        [SerializeField]
        private TemperatureUnit temperatureUnit;
        [SerializeField]
        private PressureUnit pressureUnit;
        [SerializeField]
        private MeridiemTime.Unit timeUnit;
        [SerializeField]
        private bool showSeconds = false;
        [SerializeField]
        private bool showMilliseconds = false;
        [SerializeField]
        [Range(-90, 90)]
        private float skyPitch;
        [SerializeField]
        [Range(0, 360)]
        private float skyRotation;

        private float skyRotationOffset = 0;
        private float skyPitchOffset = 0;

        [Serializable]
        public struct MonthData
        {
            public string name;
            public int days;
            public MonthData(string name, int days)
            {
                this.name = name;
                this.days = days;
            }
        }

        [SerializeField]
        private MonthData[] months = new MonthData[12] {
            new("January", 31),
            new("February", 28),
            new("March", 31),
            new("April", 30),
            new("May", 31),
            new("June", 30),
            new("July", 31),
            new("August", 31),
            new("September", 30),
            new("October", 31),
            new("November", 30),
            new("December", 31),
        };

        public enum DateFormat { MDY, DMY, YMD }
        [SerializeField]
        [Tooltip("Sets the order in which the day, month, and year are displayed when viewing the date in numeric date format (Month/Day/Year is the default)")]
        private DateFormat dateFormat = DateFormat.MDY;

        [SerializeField]
        [Tooltip("Sets the day of the week for the first day of the first month in the year 0")]
        private DayOfWeek startingWeekday = DayOfWeek.Sunday;

        [SerializeField]
        private bool displayMiscEvents = false;
        [SerializeField]
        private bool displayWeatherEvents = true;
        [SerializeField]
        private bool displayHolidayEvents = true;
        [SerializeField]
        private bool displayStatusEvents = true;
        [SerializeField]
        private bool displayWorldEventEvents = true;
        [SerializeField]
        private bool displayPlayerEvents = true;
        [SerializeField]
        private bool displayNPCEvents = true;
        [SerializeField]
        private bool displayEnemyEvents = true;
        [SerializeField]
        private bool displayAmbientEvents = true;

        [SerializeField]
        private bool handleSceneLighting = true;
        [SerializeField]
        private bool updateInEditMode = true;
        [SerializeField]
        private bool fog = true;
        [SerializeField]
        private bool followCamera = true;
        [SerializeField]
        private bool gizmos = true;
        [SerializeField]
        private bool tooltips = true;
        [SerializeField]
        private bool graphs = true;
        [SerializeField]
        private bool dontDestroyWeatherOnLoad = false;
        [SerializeField]
        private bool renderFXBlocking = true;
        [SerializeField]
        private float fxRenderDistance = 25;
        [SerializeField]
        private float fxRenderHeight = 25;
        [SerializeField]
        [Tooltip("Specify the resolution of the fx block texture. Higher number yield better quality results but lower performance.")]
        private int fxBlockingResolution = 256;
        [SerializeField]
        private LayerMask fxBlockingLayerMask = 0;
        public static int FXBlockingRendererIndex
        {
            get => Instance.fxBlockingRendererIndex;
            set => Instance.fxBlockingRendererIndex = value;
        }
        [SerializeField]
        private int fxBlockingRendererIndex = 1;



        [SerializeField]
        private CozyStyle[] styles;
        private int styleSelection;

        [SerializeField]
        private MeridiemBlock dawnTimes = new(new MeridiemTime(4, 00, Meridiem.AM), new MeridiemTime(5, 30, Meridiem.AM));
        [SerializeField]
        private MeridiemBlock morningTimes = new(new MeridiemTime(6, 00, Meridiem.AM), new MeridiemTime(7, 00, Meridiem.AM));
        [SerializeField]
        private MeridiemBlock dayTimes = new(new MeridiemTime(7, 30, Meridiem.AM), new MeridiemTime(9, 00, Meridiem.AM));
        [SerializeField]
        private MeridiemBlock afternoonTimes = new(new MeridiemTime(1, 00, Meridiem.PM), new MeridiemTime(3, 30, Meridiem.PM));
        [SerializeField]
        private MeridiemBlock eveningTimes = new(new MeridiemTime(5, 00, Meridiem.PM), new MeridiemTime(6, 30, Meridiem.PM));
        [SerializeField]
        private MeridiemBlock twilightTimes = new(new MeridiemTime(7, 00, Meridiem.PM), new MeridiemTime(8, 00, Meridiem.PM));
        [SerializeField]
        private MeridiemBlock nightTimes = new(new MeridiemTime(9, 00, Meridiem.PM), new MeridiemTime(10, 00, Meridiem.PM));

        public enum Downsampling { x1 = 1, x2 = 2, x4 = 4 }
        public Downsampling cloudDownsampling = Downsampling.x2;
        public Downsampling fogDownsampling = Downsampling.x2;

        //Boilerplate
        private static CozyPreferences _instance;
        public static CozyPreferences Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<CozyPreferences>("COZY Settings");

                    if (_instance == null)
                    {
                        Debug.LogError("Settings asset 'COZY Settings' not found in any Resources folder. Please create one.");
                    }
                }
                return _instance;
            }
        }
        public static bool Setup => _instance != null;

#if UNITY_EDITOR
        public static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(Instance);
        }
#endif
    }
}
