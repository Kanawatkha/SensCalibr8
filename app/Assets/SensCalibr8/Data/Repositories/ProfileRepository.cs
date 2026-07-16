using System;
using System.Collections.Generic;
using SensCalibr8.Data.Persistence;

namespace SensCalibr8.Data.Repositories
{
    public sealed class ProfileRepository
    {
        private readonly RepositoryExecution execution;

        public ProfileRepository(SqliteConnectionFactory connectionFactory, IDataFailureReporter failureReporter = null)
        { execution = new RepositoryExecution(connectionFactory, failureReporter); }

        public ProfileRecord Create(ProfileRecord profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            return execution.Write("create profile", connection =>
            {
                connection.Execute(@"INSERT INTO profiles(name, created_date, mouse_dpi, current_sensitivity, configured_polling_rate_hz,
dominant_hand, crosshair_config, grip_style, movement_strategy, mousepad_width_cm, mousepad_height_cm, ads_multiplier, last_active_date)
VALUES (@name,@created_date,@mouse_dpi,@current_sensitivity,@configured_polling_rate_hz,@dominant_hand,@crosshair_config,
@grip_style,@movement_strategy,@mousepad_width_cm,@mousepad_height_cm,@ads_multiplier,@last_active_date);", Parameters(profile));
                return WithId(profile, connection.LastInsertRowId());
            });
        }

        public ProfileRecord FindById(long id)
        {
            return execution.Read("read profile", connection =>
            {
                IReadOnlyList<IReadOnlyDictionary<string, object>> rows = connection.Query("SELECT * FROM profiles WHERE id=@id;", new Dictionary<string, object> { ["@id"] = id });
                return rows.Count == 0 ? null : Map(rows[0]);
            });
        }

        public ProfileRecord FindByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Profile name is required.", nameof(name));
            return execution.Read("read profile by name", connection =>
            {
                IReadOnlyList<IReadOnlyDictionary<string, object>> rows = connection.Query("SELECT * FROM profiles WHERE name=@name;", new Dictionary<string, object> { ["@name"] = name });
                return rows.Count == 0 ? null : Map(rows[0]);
            });
        }

        public IReadOnlyList<ProfileRecord> List()
        {
            return execution.Read("list profiles", connection =>
            {
                var result = new List<ProfileRecord>();
                foreach (IReadOnlyDictionary<string, object> row in connection.Query("SELECT * FROM profiles ORDER BY id;")) result.Add(Map(row));
                return result;
            });
        }

        public ProfileRecord UpdatePreservingCrosshair(ProfileRecord profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!profile.Id.HasValue) throw new ArgumentException("Profile id is required for update.", nameof(profile));
            return execution.Write("update profile", connection =>
            {
                int changed = connection.Execute(@"UPDATE profiles SET name=@name, mouse_dpi=@mouse_dpi,
current_sensitivity=@current_sensitivity, configured_polling_rate_hz=@configured_polling_rate_hz,
dominant_hand=@dominant_hand, grip_style=@grip_style, movement_strategy=@movement_strategy,
mousepad_width_cm=@mousepad_width_cm, mousepad_height_cm=@mousepad_height_cm,
ads_multiplier=@ads_multiplier, last_active_date=@last_active_date WHERE id=@id;", UpdateParameters(profile));
                if (changed != 1) throw new InvalidOperationException("Profile update did not affect exactly one row.");
                IReadOnlyList<IReadOnlyDictionary<string, object>> rows = connection.Query("SELECT * FROM profiles WHERE id=@id;", new Dictionary<string, object> { ["@id"] = profile.Id.Value });
                if (rows.Count != 1) throw new InvalidOperationException("Updated profile could not be read.");
                return Map(rows[0]);
            });
        }

        public bool DeleteById(long id)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            return execution.Write("delete profile", connection => connection.Execute("DELETE FROM profiles WHERE id=@id;", new Dictionary<string, object> { ["@id"] = id }) == 1);
        }

        private static IReadOnlyDictionary<string, object> Parameters(ProfileRecord value) => new Dictionary<string, object>
        {
            ["@name"] = value.Name, ["@created_date"] = value.CreatedDate, ["@mouse_dpi"] = value.MouseDpi,
            ["@current_sensitivity"] = value.CurrentSensitivity, ["@configured_polling_rate_hz"] = value.ConfiguredPollingRateHz,
            ["@dominant_hand"] = value.DominantHand, ["@crosshair_config"] = value.CrosshairConfig, ["@grip_style"] = value.GripStyle,
            ["@movement_strategy"] = value.MovementStrategy, ["@mousepad_width_cm"] = value.MousepadWidthCm,
            ["@mousepad_height_cm"] = value.MousepadHeightCm, ["@ads_multiplier"] = value.AdsMultiplier, ["@last_active_date"] = value.LastActiveDate
        };

        private static IReadOnlyDictionary<string, object> UpdateParameters(ProfileRecord value) => new Dictionary<string, object>
        {
            ["@id"] = value.Id.Value, ["@name"] = value.Name, ["@mouse_dpi"] = value.MouseDpi,
            ["@current_sensitivity"] = value.CurrentSensitivity, ["@configured_polling_rate_hz"] = value.ConfiguredPollingRateHz,
            ["@dominant_hand"] = value.DominantHand, ["@grip_style"] = value.GripStyle,
            ["@movement_strategy"] = value.MovementStrategy, ["@mousepad_width_cm"] = value.MousepadWidthCm,
            ["@mousepad_height_cm"] = value.MousepadHeightCm, ["@ads_multiplier"] = value.AdsMultiplier,
            ["@last_active_date"] = value.LastActiveDate
        };

        private static ProfileRecord Map(IReadOnlyDictionary<string, object> row) => new ProfileRecord(
            Int64(row, "id"), Text(row, "name"), Text(row, "created_date"), Int64(row, "mouse_dpi"), Double(row, "current_sensitivity"),
            Double(row, "configured_polling_rate_hz"), Text(row, "dominant_hand"), Text(row, "crosshair_config"), Text(row, "grip_style"),
            Text(row, "movement_strategy"), Double(row, "mousepad_width_cm"), Double(row, "mousepad_height_cm"), Double(row, "ads_multiplier"), Text(row, "last_active_date"));
        private static ProfileRecord WithId(ProfileRecord value, long id) => new ProfileRecord(id, value.Name, value.CreatedDate, value.MouseDpi, value.CurrentSensitivity, value.ConfiguredPollingRateHz, value.DominantHand, value.CrosshairConfig, value.GripStyle, value.MovementStrategy, value.MousepadWidthCm, value.MousepadHeightCm, value.AdsMultiplier, value.LastActiveDate);
        private static long Int64(IReadOnlyDictionary<string, object> row, string key) => Convert.ToInt64(row[key]);
        private static double Double(IReadOnlyDictionary<string, object> row, string key) => Convert.ToDouble(row[key]);
        private static string Text(IReadOnlyDictionary<string, object> row, string key) => Convert.ToString(row[key]);
    }
}
