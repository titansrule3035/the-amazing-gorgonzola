using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class SaveManager
{
    private static readonly string SAVE_PATH = Directory.GetCurrentDirectory() + "//save.dat";
    private const string MASTER_SECRET =
        "TheAmazingGorgonzola_SuperSecret_2026";

    public static void SaveGame(GlobalGameManager ggm)
    {
        SaveData saveData = new SaveData
        {
            activeLevelIndex = ggm.activeLevelIndex,
            completedWorlds = ggm.completedWorlds,
            deaths = ggm.deaths,
            clonesKilled = ggm.clonesKilled,
            collectibles = new List<string>
                {
                    "Pickup1",
                    "Pickup2",
                    "Pickup3"
                }
        };

        byte[] plaintext =
            JsonSerializer.SerializeToUtf8Bytes(saveData);

        byte[] salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        byte[] nonce = System.Security.Cryptography.RandomNumberGenerator.GetBytes(12);

        using var pbkdf2 = new Rfc2898DeriveBytes(
            MASTER_SECRET,
            salt,
            100000,
            HashAlgorithmName.SHA256);

        byte[] key = pbkdf2.GetBytes(32);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[16];

        using (var aes = new AesGcm(key))
        {
            aes.Encrypt(
                nonce,
                plaintext,
                ciphertext,
                tag);
        }

        using BinaryWriter writer =
            new BinaryWriter(File.Create(SAVE_PATH));

        writer.Write(new char[] { 'G', 'O', 'R', 'G' });
        writer.Write(1);

        writer.Write(salt.Length);
        writer.Write(salt);

        writer.Write(nonce.Length);
        writer.Write(nonce);

        writer.Write(tag.Length);
        writer.Write(tag);

        writer.Write(ciphertext.Length);
        writer.Write(ciphertext);
    }

    public static SaveData? LoadGame()
    {
        if (!File.Exists(SAVE_PATH))
        {
            return null;
        }
        using BinaryReader reader = new BinaryReader(File.OpenRead(SAVE_PATH));

        string magic = new string(reader.ReadChars(4));

        if (magic != "GORG")
        {
            return null;
        }

        int version = reader.ReadInt32();

        int saltLength = reader.ReadInt32();
        byte[] salt = reader.ReadBytes(saltLength);

        int nonceLength = reader.ReadInt32();
        byte[] nonce = reader.ReadBytes(nonceLength);

        int tagLength = reader.ReadInt32();
        byte[] tag = reader.ReadBytes(tagLength);

        int cipherLength = reader.ReadInt32();
        byte[] ciphertext = reader.ReadBytes(cipherLength);

        using var pbkdf2 = new Rfc2898DeriveBytes(
            MASTER_SECRET,
            salt,
            100000,
            HashAlgorithmName.SHA256);

        byte[] key = pbkdf2.GetBytes(32);

        byte[] plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(key);

            aes.Decrypt(
                nonce,
                ciphertext,
                tag,
                plaintext);
        }
        catch (CryptographicException)
        {
            return null;
        }

        return JsonSerializer.Deserialize<SaveData>(plaintext);
    }

    public static int LoadCompletedLevels()
    {
        SaveData? data = LoadGame();
        return data?.activeLevelIndex ?? 0;
    }

    public static int LoadCompletedWorlds()
    {
        SaveData? data = LoadGame();
        return data?.completedWorlds ?? 0;
    }

    public static List<string> LoadCompletedCollectibles()
    {
        SaveData? data = LoadGame();
        return data?.collectibles ?? null;
    }

    public static void DeleteSave()
    {
        if (File.Exists(SAVE_PATH))
        {
            File.Delete(SAVE_PATH);
        }
    }
}

public class SaveData
{
    public int activeLevelIndex { get; set; }
    public int completedWorlds { get; set; }
    public int deaths { get; set; }
    public int clonesKilled { get; set; }
    public List<string> collectibles { get; set; } = new();

    public SaveData()
    {

    }

    public SaveData(
        int activeLevelIndex,
        int completedWorlds,
        int deaths,
        int clonesKilled,
        List<string> collectibles)
    {
        this.activeLevelIndex = activeLevelIndex;
        this.completedWorlds = completedWorlds;
        this.collectibles = collectibles;
        this.deaths = deaths;
        this.clonesKilled = clonesKilled;
    }
}
