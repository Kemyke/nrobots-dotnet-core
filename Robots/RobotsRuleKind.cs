namespace Robots
{
    /// <summary>
    /// Whether a <see cref="RobotsRule"/> allows or disallows the paths it matches.
    /// </summary>
    public enum RobotsRuleKind
    {
        /// <summary>An Allow rule.</summary>
        Allow,

        /// <summary>A Disallow rule.</summary>
        Disallow,
    }
}
