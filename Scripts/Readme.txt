Core/                # Game.Core.asmdef
│   │   │   │   ├── Game.Core.asmdef
│   │   │   │   ├── Constants/
│   │   │   │   │   ├── GameConstants.cs
│   │   │   │   │   ├── LayerMasks.cs
│   │   │   │   │   ├── Tags.cs
│   │   │   │   │   └── SceneNames.cs
│   │   │   │   ├── Enums/
│   │   │   │   │   ├── GameState.cs
│   │   │   │   │   ├── CharacterState.cs
│   │   │   │   │   ├── ItemType.cs
│   │   │   │   │   └── DamageType.cs
│   │   │   │   ├── Interfaces/
│   │   │   │   │   ├── ICharacter.cs
│   │   │   │   │   ├── IDamageable.cs
│   │   │   │   │   ├── IInteractable.cs
│   │   │   │   │   ├── ISaveable.cs
│   │   │   │   │   └── IPoolable.cs
│   │   │   │   ├── Events/
│   │   │   │   │   ├── GameEvents.cs
│   │   │   │   │   └── EventTypes.cs
│   │   │   │   ├── DataTypes/
│   │   │   │   │   ├── Stats.cs
│   │   │   │   │   ├── Range.cs
│   │   │   │   │   └── SerializableTypes/
│   │   │   │   └── Extensions/
│   │   │   │       ├── MathExtensions.cs
│   │   │   │       └── StringExtensions.cs
│   │   │   │ 	├── Network/             # Game.Network.asmdef (선택적)
│   │   │   │  	 	├── Game.Network.asmdef
│   │   │   │   	├── Protocols/
│   │   │   │   	├── Handlers/
│   │   │   │   	└── Services/
│   │   │   ├── Utilities/           # Game.Utilities.asmdef
│   │   │   │   ├── Game.Utilities.asmdef
│   │   │   │   ├── Patterns/
│   │   │   │   │   ├── Singleton/
│   │   │   │   │   ├── StateMachine/
│   │   │   │   │   ├── Command/
│   │   │   │   │   └── Observer/
│   │   │   │   ├── Pooling/
│   │   │   │   │   ├── ObjectPool.cs
│   │   │   │   │   └── PoolManager.cs
│   │   │   │   ├── Timers/
│   │   │   │   │   ├── Timer.cs
│   │   │   │   │   └── CountdownTimer.cs
│   │   │   │   ├── Math/
│   │   │   │   │   ├── MathUtility.cs
│   │   │   │   │   └── RandomUtility.cs
│   │   │   │   ├── Collections/
│   │   │   │   │   ├── CircularBuffer.cs
│   │   │   │   │   └── SerializableDictionary.cs
│   │   │   │   └── Debug/
│   │   │   │       ├── DebugDraw.cs
│   │   │   │       └── FPSCounter.cs
│   │   │   │
│   │   │   ├── Data/                # Game.Data.asmdef
│   │   │   │   ├── Game.Data.asmdef
│   │   │   │   ├── Models/
│   │   │   │   │   ├── ItemData.cs
│   │   │   │   │   ├── CharacterData.cs
│   │   │   │   │   └── SkillData.cs
│   │   │   │   ├── ScriptableObjects/
│   │   │   │   │   ├── ItemSO.cs
│   │   │   │   │   ├── CharacterSO.cs
│   │   │   │   │   └── GameSettingsSO.cs
│   │   │   │   └── Configurations/
│   │   │   │       ├── GameConfig.cs
│   │   │   │       └── BalanceConfig.cs
│   │   │   │
│   │   │   ├── Gameplay/            # Game.Gameplay.asmdef
│   │   │   │   ├── Game.Gameplay.asmdef
│   │   │   │   ├── Character/
│   │   │   │   │   ├── Player/
│   │   │   │   │   │   ├── PlayerController.cs
│   │   │   │   │   │   └── PlayerStateMachine.cs
│   │   │   │   │   ├── Enemy/
│   │   │   │   │   │   ├── EnemyAI.cs
│   │   │   │   │   │   └── EnemyBehavior.cs
│   │   │   │   │   └── NPC/
│   │   │   │   ├── Combat/
│   │   │   │   │   ├── DamageSystem.cs
│   │   │   │   │   ├── SkillSystem.cs
│   │   │   │   │   └── Projectiles/
│   │   │   │   ├── Items/
│   │   │   │   │   ├── Inventory.cs
│   │   │   │   │   ├── ItemPickup.cs
│   │   │   │   │   └── Equipment.cs
│   │   │   │   └── World/
│   │   │   │       ├── Interactables/
│   │   │   │       └── Triggers/
│   │   │   │
│   │   │   │
│   │   │   ├── Editor/              # Game.Editor.asmdef
│   │   │   │   ├── Game.Editor.asmdef
│   │   │   │   ├── Tools/
│   │   │   │   ├── Inspectors/
│   │   │   │   └── Windows/
│   │   │   │
│   │   │   └── Tests/
│   │   │       ├── PlayMode/        # Game.Tests.PlayMode.asmdef
│   │   │       └── EditMode/        # Game.Tests.EditMode.asmdef