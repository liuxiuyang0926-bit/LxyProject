using System;
using System.Collections.Generic;
using System.IO;
using config;
using Luban;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    internal sealed class TurnBasedConfigUnitOption
    {
        public long Id;
        public string Name;
        public long EntityId;
        public string ModelPath;
        public Game.Battle.TurnBased.Authoring.BattleFormationUnitSource Source;

        public string Label => $"{Id}  {Name}";
        public string AssetPath => string.IsNullOrWhiteSpace(ModelPath)
            ? string.Empty
            : "Assets/GameResources/" + ModelPath.TrimStart('/', '\\');
    }

    internal static class TurnBasedConfigEditorCatalog
    {
        private const string RelativeDataRoot =
            "Assets/GameResources/GameData/ExcelData";

        public static readonly List<TurnBasedConfigUnitOption> Heroes =
            new List<TurnBasedConfigUnitOption>();
        public static readonly List<TurnBasedConfigUnitOption> Monsters =
            new List<TurnBasedConfigUnitOption>();

        public static string LastError { get; private set; }

        public static bool Reload()
        {
            Heroes.Clear();
            Monsters.Clear();
            LastError = string.Empty;
            try
            {
                string root = Path.GetFullPath(RelativeDataRoot);
                Tables.Initialize(name =>
                {
                    string path = Path.Combine(root, name + ".bytes");
                    if (!File.Exists(path))
                    {
                        throw new FileNotFoundException(
                            $"缺少 Luban 二进制表：{path}",
                            path);
                    }
                    return new ByteBuf(File.ReadAllBytes(path));
                });
                Tables.PreloadAll();

                EntityModel models = Tables.EntityModel.Data;
                List<HeroDataModel> heroes = Tables.Hero.Data.DataList;
                for (int index = 0; index < heroes.Count; index++)
                {
                    HeroDataModel hero = heroes[index];
                    EntityModelDataModel model = models.GetOrDefault(
                        checked((int)hero.EntityId));
                    Heroes.Add(new TurnBasedConfigUnitOption
                    {
                        Id = hero.Id,
                        Name = hero.Name,
                        EntityId = hero.EntityId,
                        ModelPath = model?.ModelPath,
                        Source = Game.Battle.TurnBased.Authoring.BattleFormationUnitSource.Hero,
                    });
                }

                List<MonsterDataModel> monsters = Tables.Monster.Data.DataList;
                for (int index = 0; index < monsters.Count; index++)
                {
                    MonsterDataModel monster = monsters[index];
                    EntityModelDataModel model = models.GetOrDefault(
                        checked((int)monster.EntityId));
                    Monsters.Add(new TurnBasedConfigUnitOption
                    {
                        Id = monster.Id,
                        Name = monster.Name,
                        EntityId = monster.EntityId,
                        ModelPath = model?.ModelPath,
                        Source = Game.Battle.TurnBased.Authoring.BattleFormationUnitSource.Monster,
                    });
                }

                Heroes.Sort((left, right) => left.Id.CompareTo(right.Id));
                Monsters.Sort((left, right) => left.Id.CompareTo(right.Id));
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Debug.LogException(exception);
                return false;
            }
        }

        public static IReadOnlyList<TurnBasedConfigUnitOption> GetOptions(
            Game.Battle.TurnBased.Authoring.BattleFormationUnitSource source)
        {
            switch (source)
            {
                case Game.Battle.TurnBased.Authoring.BattleFormationUnitSource.Hero:
                    return Heroes;
                case Game.Battle.TurnBased.Authoring.BattleFormationUnitSource.Monster:
                    return Monsters;
                default:
                    return Array.Empty<TurnBasedConfigUnitOption>();
            }
        }

        public static TurnBasedConfigUnitOption Find(
            Game.Battle.TurnBased.Authoring.BattleFormationUnitSource source,
            long id)
        {
            IReadOnlyList<TurnBasedConfigUnitOption> options = GetOptions(source);
            for (int index = 0; index < options.Count; index++)
            {
                if (options[index].Id == id)
                {
                    return options[index];
                }
            }
            return null;
        }

        public static GameObject LoadModelPrefab(TurnBasedConfigUnitOption option)
        {
            if (option == null || string.IsNullOrWhiteSpace(option.AssetPath))
            {
                return null;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(option.AssetPath);
            if (prefab != null)
            {
                return prefab;
            }

            string fileName = Path.GetFileNameWithoutExtension(option.ModelPath);
            string[] guids = AssetDatabase.FindAssets(fileName + " t:Prefab");
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        fileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }
            return null;
        }
    }
}
