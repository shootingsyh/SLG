using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SLG.Saves
{
    public static class SaveSerializer
    {
        public static string SerializePayload<T>(string saveType, T payload, string savedAtUtc = null)
        {
            string payloadJson = JsonUtility.ToJson(payload, true);
            SaveFileEnvelope envelope = new SaveFileEnvelope
            {
                FormatVersion = SaveConstants.FormatVersion,
                SaveType = saveType,
                GameVersion = SaveConstants.GameVersion,
                ContentVersion = SaveConstants.ContentVersion,
                SavedAtUtc = savedAtUtc ?? DateTime.UtcNow.ToString("O"),
                PayloadJson = payloadJson,
                Checksum = ComputeChecksum(saveType, payloadJson)
            };
            return JsonUtility.ToJson(envelope, true);
        }

        public static bool TryDeserializePayload<T>(string json, string expectedType, out T payload, out SaveMetadata metadata, out SaveSlotState state, out string error)
        {
            payload = default;
            metadata = null;
            state = SaveSlotState.Corrupt;
            error = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    state = SaveSlotState.Corrupt;
                    error = "Save file is empty.";
                    return false;
                }

                SaveFileEnvelope envelope = JsonUtility.FromJson<SaveFileEnvelope>(json);
                if (envelope == null || string.IsNullOrWhiteSpace(envelope.SaveType) || string.IsNullOrWhiteSpace(envelope.PayloadJson))
                {
                    state = SaveSlotState.MissingContent;
                    error = "Save envelope is missing required content.";
                    return false;
                }

                metadata = new SaveMetadata
                {
                    SaveType = envelope.SaveType,
                    SavedAtUtc = envelope.SavedAtUtc,
                    FormatVersion = envelope.FormatVersion,
                    VersionStatus = envelope.FormatVersion == SaveConstants.FormatVersion ? "Current" : "Unsupported"
                };

                if (envelope.FormatVersion != SaveConstants.FormatVersion)
                {
                    state = SaveSlotState.UnsupportedVersion;
                    error = "Unsupported save format version.";
                    return false;
                }

                if (envelope.SaveType != expectedType)
                {
                    state = SaveSlotState.MissingContent;
                    error = $"Wrong save type '{envelope.SaveType}'.";
                    return false;
                }

                string checksum = ComputeChecksum(envelope.SaveType, envelope.PayloadJson);
                if (checksum != envelope.Checksum)
                {
                    state = SaveSlotState.ChecksumMismatch;
                    error = "Save checksum mismatch.";
                    return false;
                }

                payload = JsonUtility.FromJson<T>(envelope.PayloadJson);
                if (payload == null)
                {
                    state = SaveSlotState.MissingContent;
                    error = "Save payload is missing.";
                    return false;
                }

                state = SaveSlotState.Valid;
                return true;
            }
            catch (Exception ex)
            {
                state = SaveSlotState.Corrupt;
                error = ex.Message;
                return false;
            }
        }

        public static string ComputeChecksum(string saveType, string payloadJson)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes((saveType ?? string.Empty) + "|" + (payloadJson ?? string.Empty));
            byte[] hash = sha.ComputeHash(bytes);
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
