namespace MixItUp.Base.Model.Twitch.Bits
{
    /// <summary>
    /// Information about a custom Power-up.
    /// </summary>
    public class CustomPowerUpModel
    {
        /// <summary>
        /// ID of the broadcaster that owns the Power-up.
        /// </summary>
        public string broadcaster_id { get; set; }
        /// <summary>
        /// Login name of the broadcaster that owns the Power-up.
        /// </summary>
        public string broadcaster_login { get; set; }
        /// <summary>
        /// Display name of the broadcaster that owns the Power-up.
        /// </summary>
        public string broadcaster_name { get; set; }
        /// <summary>
        /// The ID of the Power-up.
        /// </summary>
        public string id { get; set; }
        /// <summary>
        /// The title of the Power-up.
        /// </summary>
        public string title { get; set; }
        /// <summary>
        /// The text prompt for the user when redeeming the Power-up.
        /// </summary>
        public string prompt { get; set; }
        /// <summary>
        /// The cost of the Power-up in Bits.
        /// </summary>
        public int bits { get; set; }
        /// <summary>
        /// The custom image of the Power-up.
        /// </summary>
        public CustomPowerUpImageModel image { get; set; }
        /// <summary>
        /// The default image of the Power-up.
        /// </summary>
        public CustomPowerUpImageModel default_image { get; set; }
        /// <summary>
        /// The HTML background color of the Power-up.
        /// </summary>
        public string background_color { get; set; }
        /// <summary>
        /// Whether the Power-up is enabled.
        /// </summary>
        public bool is_enabled { get; set; }
        /// <summary>
        /// Whether user input is required when redeeming the Power-up.
        /// </summary>
        public bool is_user_input_required { get; set; }
        /// <summary>
        /// The maximum per-stream redemption settings for the Power-up.
        /// </summary>
        public CustomPowerUpMaxPerStreamSettingModel max_per_stream_setting { get; set; }
        /// <summary>
        /// The maximum per-user, per-stream redemption settings for the Power-up.
        /// </summary>
        public CustomPowerUpMaxPerUserPerStreamSettingModel max_per_user_per_stream_setting { get; set; }
        /// <summary>
        /// The global cooldown for the Power-up.
        /// </summary>
        public CustomPowerUpGlobalCooldownSettingModel global_cooldown_setting { get; set; }
        /// <summary>
        /// Whether the Power-up is currently paused.
        /// </summary>
        public bool is_paused { get; set; }
        /// <summary>
        /// Whether the Power-up currently has stock to be redeemed.
        /// </summary>
        public bool is_in_stock { get; set; }
        /// <summary>
        /// The number of redemptions redeemed during the current live stream.
        /// </summary>
        public int? redemptions_redeemed_current_stream { get; set; }
        /// <summary>
        /// Timestamp of the cooldown expiration. Null if the Power-up isn't on cooldown.
        /// </summary>
        public string cooldown_expires_at { get; set; }
    }

    /// <summary>
    /// Information about a custom Power-up's image.
    /// </summary>
    public class CustomPowerUpImageModel
    {
        /// <summary>
        /// The 1x image URL.
        /// </summary>
        public string url_1x { get; set; }
        /// <summary>
        /// The 2x image URL.
        /// </summary>
        public string url_2x { get; set; }
        /// <summary>
        /// The 4x image URL.
        /// </summary>
        public string url_4x { get; set; }
    }

    /// <summary>
    /// Information about a custom Power-up's max per-stream settings.
    /// </summary>
    public class CustomPowerUpMaxPerStreamSettingModel
    {
        /// <summary>
        /// Whether it is enabled.
        /// </summary>
        public bool is_enabled { get; set; }
        /// <summary>
        /// The maximum times per stream this Power-up can be redeemed.
        /// </summary>
        public long max_per_stream { get; set; }
    }

    /// <summary>
    /// Information about a custom Power-up's max per-user, per-stream settings.
    /// </summary>
    public class CustomPowerUpMaxPerUserPerStreamSettingModel
    {
        /// <summary>
        /// Whether it is enabled.
        /// </summary>
        public bool is_enabled { get; set; }
        /// <summary>
        /// The maximum times an individual user can redeem this Power-up per stream.
        /// </summary>
        public long max_per_user_per_stream { get; set; }
    }

    /// <summary>
    /// Information about a custom Power-up's global cooldown settings.
    /// </summary>
    public class CustomPowerUpGlobalCooldownSettingModel
    {
        /// <summary>
        /// Whether it is enabled.
        /// </summary>
        public bool is_enabled { get; set; }
        /// <summary>
        /// The amount of seconds for the global cooldown.
        /// </summary>
        public long global_cooldown_seconds { get; set; }
    }
}
