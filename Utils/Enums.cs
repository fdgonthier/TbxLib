namespace TeamboxLib.Utils
{
    /// <summary>
    /// Processing status of an ANP event. These statuses are used in the 
    /// database.
    /// </summary>
    public enum KwsAnpEventStatus
    {
        /// <summary>
        /// The event has not been processed yet.
        /// </summary>
        Unprocessed = 0,

        /// <summary>
        /// The event was processed successfully.
        /// </summary>
        Processed
    }
}