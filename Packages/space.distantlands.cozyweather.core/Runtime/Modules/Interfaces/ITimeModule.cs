//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

namespace DistantLands.Cozy
{
    /// <summary>
    /// Add time modification methods that will be used when the modified time (sent to the atmosphere) is calculated. 
    /// Useful for modifying the time of day that certain solar events (such as sunrise and sunset) occur.
    /// </summary>
    /// <param name="input">The time before modification</param>
    /// <returns>The time after modification</returns>
    public delegate float ModifyTime(float input);
    /// <summary>
    /// Action that runs when a new hour is reached.
    /// </summary>
    /// <param name="oldHour">The hour in military time that the time changed from.</param>
    /// <param name="newHour">The hour in military time that the time changed to.</param>
    public delegate void NewHour(int oldHour, int newHour);
    /// <summary>
    /// Action that runs when a new minute is reached.
    /// </summary>
    /// <param name="oldMinute">The hour in military time that the time changed from.</param>
    /// <param name="newMinute">The hour in military time that the time changed to.</param>
    public delegate void NewMinute(int oldMinute, int newMinute);
    /// <summary>
    /// Determines the method used to calculate and control the time. 
    /// Linear - time moves linearly based on the time movement speed
    /// Via Scripting - time is paused unless moved. Set the CurrentTime to control this
    /// System Time - automatically sets the time to the system time of your computer. 
    /// </summary>
    public enum ControlTime
    {
        Linear,
        ViaScripting,
        SystemTime
    }
    /// <summary>
    /// Interface for any module that controls time for COZY.
    /// </summary>
    public interface ITimeModule : ICozyModule
    {
        /// <summary>
        /// Access the current time of day.
        /// </summary>
        public MeridiemTime CurrentTime { get; set; }
        /// <summary>
        /// Access the perceived time of day based on the sun. One full day is 1.0. This is modified by adding Actions to the OnModifyTime event. Always 0 at midnight, 0.25 at sunrise, 0.50 at noon, and 0.75 at sunset regardless of the modifications made.
        /// </summary>
        public float ModifiedTime { get; }
        /// <summary>
        /// Specifies the amount of in-game minutes that pass in a real-world second.
        /// </summary>
        public float TimeMovementSpeed { get; set; }

        /// <summary>
        /// Returns the index of the current day in the with the time added as a decimal. For example, 6 AM on March 3rd will return 62.25
        /// </summary>
        public float DayAndTime { get; }

        /// <summary>
        /// Determines if time is currently paused
        /// </summary>
        public bool PauseTime { get; set; }

        /// <summary>
        /// Add time modification methods that will be used when the modified time (sent to the atmosphere) is calculated. 
        /// Useful for modifying the time of day that certain solar events (such as sunrise and sunset) occur.
        /// </summary>
        public event ModifyTime OnModifyTime;

        /// <summary>
        /// Event that runs every hour
        /// </summary>
        public event NewHour OnNewHour;

        /// <summary>
        /// Event that runs every minute
        /// </summary>
        public event NewMinute OnNewMinute;



    }
}