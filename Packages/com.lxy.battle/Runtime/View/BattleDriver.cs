using Game.Battle.Client;
using Game.Battle.Config;
using Game.Battle.Core;
using Game.Battle.Core.Math;
using UnityEngine;

namespace Game.Battle.View
{
    /// <summary>
    /// 本地帧同步 Demo 入口。Unity Update 只负责采集输入和安排逻辑帧，
    /// 所有战斗结果均由 BattleWorld.Tick(FrameData) 决定。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("LxyDemo/Battle/Battle Driver")]
    public sealed class BattleDriver : MonoBehaviour
    {
        private const int PlayerEntityId = 1;
        private const int EnemyEntityId = 2;
        private const int PlayerId = 1001;
        private const int EnemyPlayerId = 1002;

        [Header("战斗配置")]
        [SerializeField]
        private BattleConfigDatabase database;

        [SerializeField]
        private EntityView entityPrefab;

        [SerializeField]
        private Transform viewRoot;

        [Header("本地帧源")]
        [SerializeField]
        [Min(0)]
        private int inputDelayFrames = 2;

        [SerializeField]
        [Min(1)]
        private int playerSkillId = 1001;

        [SerializeField]
        private bool enableEnemyAutoCast = true;

        [SerializeField]
        private bool initializeOnStart = true;

        private BattleWorld world;
        private FrameBuffer frameBuffer;
        private LocalFrameSession localSession;
        private BattleClient battleClient;
        private BattleViewWorld viewWorld;
        private FPVector2 lastMoveDirection;
        private float accumulator;
        private int nextEnemyCastFrame = 30;

        public BattleWorld World => world;
        public BattleClient Client => battleClient;
        public bool IsRunning => world != null;

        private void Start()
        {
            if (initializeOnStart)
            {
                StartBattle();
            }
        }

        public void StartBattle()
        {
            StopBattle();

            BattleConfigCatalog catalog = database != null
                ? database.BuildCatalog()
                : BattleConfigDatabase.CreateDefaultCatalog();
            world = new BattleWorld(catalog, 123456);
            world.Initialize();
            world.AddEntity(
                PlayerEntityId,
                PlayerId,
                1,
                new FPVector2(
                    FP.FromRaw(-15000),
                    FP.Zero),
                1000,
                FP.FromInt(4));
            world.AddEntity(
                EnemyEntityId,
                EnemyPlayerId,
                2,
                new FPVector2(
                    FP.FromRaw(15000),
                    FP.Zero),
                1000,
                FP.FromInt(3));

            if (viewRoot == null)
            {
                var rootObject = new GameObject("BattleViews");
                rootObject.transform.SetParent(transform, false);
                viewRoot = rootObject.transform;
            }

            viewWorld = new BattleViewWorld(
                world,
                entityPrefab,
                viewRoot);
            viewWorld.CreateEntityView(PlayerEntityId, Color.cyan);
            viewWorld.CreateEntityView(EnemyEntityId, Color.red);

            frameBuffer = new FrameBuffer();
            localSession = new LocalFrameSession();
            battleClient = new BattleClient(
                world,
                frameBuffer,
                viewWorld);
            accumulator = 0f;
            lastMoveDirection = FPVector2.Zero;
            nextEnemyCastFrame = 30;
        }

        public void StopBattle()
        {
            viewWorld?.Dispose();
            viewWorld = null;
            battleClient = null;
            localSession = null;
            frameBuffer = null;
            world = null;
            accumulator = 0f;
        }

        private void Update()
        {
            if (world == null)
            {
                return;
            }

            CaptureLocalInput();
            QueueEnemyInput();

            accumulator += Time.unscaledDeltaTime;
            float logicDelta = 1f / BattleConst.LogicFps;
            int tickCount = 0;
            while (accumulator >= logicDelta && tickCount < 8)
            {
                accumulator -= logicDelta;
                FrameDataToBuffer(world.CurrentFrame);
                battleClient.ConsumeAvailableFrames(1);
                tickCount++;
            }

            viewWorld.TickVisual(Time.unscaledDeltaTime);
        }

        private void CaptureLocalInput()
        {
            int horizontal = 0;
            int vertical = 0;
            if (Input.GetKey(KeyCode.A) ||
                Input.GetKey(KeyCode.LeftArrow))
            {
                horizontal--;
            }
            if (Input.GetKey(KeyCode.D) ||
                Input.GetKey(KeyCode.RightArrow))
            {
                horizontal++;
            }
            if (Input.GetKey(KeyCode.S) ||
                Input.GetKey(KeyCode.DownArrow))
            {
                vertical--;
            }
            if (Input.GetKey(KeyCode.W) ||
                Input.GetKey(KeyCode.UpArrow))
            {
                vertical++;
            }

            var direction = new FPVector2(
                FP.FromInt(horizontal),
                FP.FromInt(vertical));
            if (direction != lastMoveDirection)
            {
                int targetFrame =
                    world.CurrentFrame +
                    Mathf.Max(0, inputDelayFrames);
                if (direction == FPVector2.Zero)
                {
                    localSession.QueueStopMove(targetFrame, PlayerId);
                }
                else
                {
                    localSession.QueueMove(
                        targetFrame,
                        PlayerId,
                        direction);
                }

                lastMoveDirection = direction;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                localSession.QueueSkill(
                    world.CurrentFrame +
                    Mathf.Max(0, inputDelayFrames),
                    PlayerId,
                    playerSkillId,
                    EnemyEntityId,
                    world.Transforms.Get(EnemyEntityId).Position);
            }
        }

        private void QueueEnemyInput()
        {
            if (!enableEnemyAutoCast ||
                world.CurrentFrame < nextEnemyCastFrame ||
                !world.IsAlive(EnemyEntityId))
            {
                return;
            }

            localSession.QueueSkill(
                world.CurrentFrame +
                Mathf.Max(0, inputDelayFrames),
                EnemyPlayerId,
                playerSkillId,
                PlayerEntityId,
                world.Transforms.Get(PlayerEntityId).Position);
            nextEnemyCastFrame = checked(nextEnemyCastFrame + 40);
        }

        private void FrameDataToBuffer(int frame)
        {
            frameBuffer.AddFrame(localSession.BuildFrame(frame));
        }

        private void OnGUI()
        {
            if (world == null)
            {
                return;
            }

            HealthComponent playerHealth =
                world.Health.Get(PlayerEntityId);
            HealthComponent enemyHealth =
                world.Health.Get(EnemyEntityId);
            GUI.Box(new Rect(12f, 12f, 390f, 108f), string.Empty);
            GUI.Label(
                new Rect(24f, 20f, 360f, 22f),
                $"Local Lockstep | Frame {world.CurrentFrame} | " +
                $"Hash {world.CalculateStateHash():X16}");
            GUI.Label(
                new Rect(24f, 45f, 360f, 22f),
                $"Player HP {playerHealth.Current}/" +
                $"{playerHealth.Maximum}    Enemy HP " +
                $"{enemyHealth.Current}/{enemyHealth.Maximum}");
            GUI.Label(
                new Rect(24f, 70f, 360f, 40f),
                "WASD/方向键：移动    Space：释放技能\n" +
                "逻辑 20 FPS，表现按渲染帧插值");
        }

        private void OnDestroy()
        {
            StopBattle();
        }
    }
}
