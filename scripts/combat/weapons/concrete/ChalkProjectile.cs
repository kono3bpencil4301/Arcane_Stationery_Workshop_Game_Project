using Godot;
using System.Collections.Generic;

public partial class ChalkProjectile : CharacterBody2D
{
	private enum ChalkState
	{
		Intact,     // 尚未击中敌人
		Cracked,    // 已穿透第一名敌人
		Exploded
	}

	// =========================
	// 基础移动参数
	// =========================

	[ExportCategory("Movement")]

	[Export]
	public float Speed { get; set; } = 520.0f;

	[Export]
	public float MaxDistance { get; set; } = 420.0f;

	/// <summary>
	/// 首次命中后减速。
	/// </summary>
	[Export(PropertyHint.Range, "0.1,1.0,0.05")]
	public float CrackedSpeedMultiplier { get; set; } = 0.82f;

	// =========================
	// 直接伤害
	// =========================

	[ExportCategory("Direct Damage")]

	/// <summary>
	/// 第一次穿透时的伤害。
	/// </summary>
	[Export]
	public float FirstHitDamage { get; set; } = 14.0f;

	/// <summary>
	/// 第二次直接命中时的伤害。
	/// 第二目标还会受到爆炸伤害。
	/// </summary>
	[Export]
	public float SecondHitDamage { get; set; } = 20.0f;

	// =========================
	// 爆炸参数
	// =========================

	[ExportCategory("Explosion")]

	[Export]
	public float ExplosionDamage { get; set; } = 18.0f;

	[Export]
	public float ExplosionRadius { get; set; } = 52.0f;

	/// <summary>
	/// 爆炸边缘的最低伤害倍率。
	/// 中心为 100%，边缘逐渐降到 40%。
	/// </summary>
	[Export(PropertyHint.Range, "0.0,1.0,0.05")]
	public float ExplosionEdgeMultiplier { get; set; } = 0.4f;

	/// <summary>
	/// 开裂后的粉笔撞墙时，爆炸伤害倍率。
	/// </summary>
	[Export(PropertyHint.Range, "0.0,1.0,0.05")]
	public float WallExplosionMultiplier { get; set; } = 0.6f;

	/// <summary>
	/// 爆炸查询检测的敌人碰撞层。
	/// 在 Inspector 中设置为 Enemy Layer。
	/// </summary>
	[Export(PropertyHint.Layers2DPhysics)]
	public uint EnemyCollisionMask { get; set; }

	// =========================
	// 美术资源
	// =========================

	[ExportCategory("Visual Effects")]

	[Export]
	public PackedScene ExplosionVfxScene { get; set; }

	[Export]
	public PackedScene BreakVfxScene { get; set; }

	/// <summary>
	/// 爆炸后留下的粉笔灰区域。
	/// 当前可暂时只作为视觉效果。
	/// </summary>
	[Export]
	public PackedScene DustZoneScene { get; set; }

	/// <summary>
	/// 完整状态下显示的粉笔精灵。
	/// 未在 Inspector 中指定时，会按节点名自动查找。
	/// </summary>
	[Export]
	public Sprite2D ChalkNormalSprite { get; set; }

	/// <summary>
	/// 第一次命中敌人后显示的开裂粉笔精灵。
	/// 未在 Inspector 中指定时，会按节点名自动查找。
	/// </summary>
	[Export]
	public Sprite2D ChalkCrackedSprite { get; set; }

	// =========================
	// 内部状态
	// =========================

	private ChalkState _state = ChalkState.Intact;

	private Vector2 _direction = Vector2.Right;
	private float _travelledDistance;

	private Node _source;

	private AnimationPlayer _animationPlayer;

	/// <summary>
	/// 已经穿透过的敌人。
	/// 防止同一敌人因大型碰撞体或贴脸状态被计算两次。
	/// </summary>
	private readonly HashSet<ulong> _hitEnemyIds = new();

	private const int MaxCollisionsPerFrame = 4;

	public override void _Ready()
	{
		_animationPlayer =
			GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

		ChalkNormalSprite ??=
			GetNodeOrNull<Sprite2D>("ChalkNormal") ??
			GetNodeOrNull<Sprite2D>("Bullet/ChalkNormal") ??
			GetNodeOrNull<Sprite2D>("Visual/ChalkNormal");

		ChalkCrackedSprite ??=
			GetNodeOrNull<Sprite2D>("ChalkCracked") ??
			GetNodeOrNull<Sprite2D>("Bullet/ChalkCracked") ??
			GetNodeOrNull<Sprite2D>("Visual/ChalkCracked");

		ShowIntactVisual();
	}

	/// <summary>
	/// 生成粉笔后，由武器调用。
	/// </summary>
	public void Setup(Vector2 direction, Node source)
	{
		if (direction.IsZeroApprox())
		{
			direction = Vector2.Right;
		}

		_direction = direction.Normalized();
		_source = source;

		Rotation = _direction.Angle();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_state == ChalkState.Exploded)
		{
			return;
		}

		Vector2 remainingMotion =
			_direction * Speed * (float)delta;

		/*
		 * 使用循环的原因：
		 *
		 * 一帧内粉笔有可能：
		 * 1. 撞到第一只敌人
		 * 2. 穿过第一只敌人
		 * 3. 继续撞到第二只敌人
		 *
		 * 如果每帧只处理一次 MoveAndCollide，
		 * 高速状态下第二次碰撞可能被拖到下一帧。
		 */
		for (
			int i = 0;
			i < MaxCollisionsPerFrame;
			i++
		)
		{
			if (remainingMotion.LengthSquared() <= 0.0001f)
			{
				break;
			}

			KinematicCollision2D collision =
				MoveAndCollide(remainingMotion);

			if (collision == null)
			{
				_travelledDistance += remainingMotion.Length();
				break;
			}

			_travelledDistance += collision.GetTravel().Length();

			remainingMotion = collision.GetRemainder();

			Node collider = collision.GetCollider() as Node;

			if (collider == null)
			{
				BreakWithoutExplosion();
				return;
			}

			bool becameCracked = HandleCollision(collider);

			if (
				_state == ChalkState.Exploded ||
				IsQueuedForDeletion()
			)
			{
				return;
			}

			/*
			 * 如果刚刚完成第一次穿透，
			 * 本帧剩余飞行距离也同步缩短。
			 */
			if (becameCracked)
			{
				remainingMotion *= CrackedSpeedMultiplier;
			}
		}

		HandleMaxDistance();
	}

	/// <summary>
	/// 返回 true 代表本次碰撞使粉笔从完整变成开裂。
	/// </summary>
	private bool HandleCollision(Node collider)
	{
		bool isEnemy =
			collider.IsInGroup("enemy") &&
			collider is IDamageable;

		if (isEnemy)
		{
			return HandleEnemyHit(
				collider,
				(IDamageable)collider
			);
		}

		HandleObstacleHit();
		return false;
	}

	private bool HandleEnemyHit(
		Node enemyNode,
		IDamageable damageable
	)
	{
		ulong enemyId = enemyNode.GetInstanceId();

		/*
		 * 理论上第一次穿透时已经添加了碰撞例外，
		 * 这里再做一层保险。
		 */
		if (_hitEnemyIds.Contains(enemyId))
		{
			AddEnemyCollisionException(enemyNode);
			return false;
		}

		if (_state == ChalkState.Intact)
		{
			// 第一名敌人：造成较低伤害，然后穿透。
			damageable.TakeDamage(
				FirstHitDamage,
				_source,
				GlobalPosition,
				_direction
			);

			_hitEnemyIds.Add(enemyId);

			_state = ChalkState.Cracked;
			Speed *= CrackedSpeedMultiplier;

			AddEnemyCollisionException(enemyNode);
			PlayCrackedAnimation();

			return true;
		}

		if (_state == ChalkState.Cracked)
		{
			// 第二名敌人：先结算直接命中伤害。
			damageable.TakeDamage(
				SecondHitDamage,
				_source,
				GlobalPosition,
				_direction
			);

			// 随后爆炸，第二名敌人也可能吃到爆炸范围伤害。
			Explode(1.0f);
		}

		return false;
	}

	private void HandleObstacleHit()
	{
		if (_state == ChalkState.Intact)
		{
			/*
			 * 尚未穿过敌人就撞墙：
			 * 只碎裂，不触发完整爆炸。
			 */
			BreakWithoutExplosion();
			return;
		}

		if (_state == ChalkState.Cracked)
		{
			/*
			 * 已经穿过一名敌人后撞墙：
			 * 触发较弱的墙壁爆炸。
			 */
			Explode(WallExplosionMultiplier);
		}
	}

	private void HandleMaxDistance()
	{
		if (_travelledDistance < MaxDistance)
		{
			return;
		}

		if (_state == ChalkState.Cracked)
		{
			// 已开裂的粉笔在射程结束时自动炸裂。
			Explode(1.0f);
		}
		else
		{
			// 没有命中过敌人，只在远处碎裂。
			BreakWithoutExplosion();
		}
	}

	private void AddEnemyCollisionException(Node enemyNode)
	{
		/*
		 * 第一次命中后，将敌人加入碰撞例外。
		 * 这样粉笔不会卡在它的碰撞体里反复触发。
		 */
		if (enemyNode is PhysicsBody2D enemyBody)
		{
			AddCollisionExceptionWith(enemyBody);
		}
	}

	private void PlayCrackedAnimation()
	{
		if (ChalkNormalSprite != null)
		{
			ChalkNormalSprite.Visible = false;
		}

		if (ChalkCrackedSprite != null)
		{
			ChalkCrackedSprite.Visible = true;
		}

		if (
			_animationPlayer != null &&
			_animationPlayer.HasAnimation("cracked")
		)
		{
			_animationPlayer.Play("cracked");
		}
	}

	private void ShowIntactVisual()
	{
		if (ChalkNormalSprite != null)
		{
			ChalkNormalSprite.Visible = true;
		}

		if (ChalkCrackedSprite != null)
		{
			ChalkCrackedSprite.Visible = false;
		}
	}

	private void Explode(float damageMultiplier)
	{
		if (_state == ChalkState.Exploded)
		{
			return;
		}

		_state = ChalkState.Exploded;

		DealExplosionDamage(damageMultiplier);

		SpawnSceneAtCurrentPosition(ExplosionVfxScene);
		SpawnSceneAtCurrentPosition(DustZoneScene);

		QueueFree();
	}

	private void DealExplosionDamage(float damageMultiplier)
	{
		CircleShape2D explosionShape = new()
		{
			Radius = ExplosionRadius
		};

		PhysicsShapeQueryParameters2D query = new()
		{
			Shape = explosionShape,
			Transform = new Transform2D(
				0.0f,
				GlobalPosition
			),
			CollisionMask = EnemyCollisionMask,
			CollideWithBodies = true,
			CollideWithAreas = false
		};

		Godot.Collections.Array<
			Godot.Collections.Dictionary
		> results =
			GetWorld2D()
				.DirectSpaceState
				.IntersectShape(query, 64);

		/*
		 * 一个敌人可能拥有多个碰撞形状，
		 * IntersectShape 可能返回多个结果。
		 * 因此需要去重。
		 */
		HashSet<ulong> damagedEnemyIds = new();

		foreach (
			Godot.Collections.Dictionary result in results
		)
		{
			if (!result.ContainsKey("collider"))
			{
				continue;
			}

			GodotObject colliderObject =
				result["collider"].AsGodotObject();

			if (colliderObject is not Node enemyNode)
			{
				continue;
			}

			if (!enemyNode.IsInGroup("enemy"))
			{
				continue;
			}

			if (enemyNode is not IDamageable damageable)
			{
				continue;
			}

			ulong enemyId = enemyNode.GetInstanceId();

			if (!damagedEnemyIds.Add(enemyId))
			{
				continue;
			}

			float distance = 0.0f;

			if (enemyNode is Node2D enemyNode2D)
			{
				distance = GlobalPosition.DistanceTo(
					enemyNode2D.GlobalPosition
				);
			}

			float normalizedDistance = Mathf.Clamp(
				distance / ExplosionRadius,
				0.0f,
				1.0f
			);

			float falloffMultiplier = Mathf.Lerp(
				1.0f,
				ExplosionEdgeMultiplier,
				normalizedDistance
			);

			float finalDamage =
				ExplosionDamage *
				damageMultiplier *
				falloffMultiplier;

			damageable.TakeDamage(
				finalDamage,
				_source,
				GlobalPosition,
				_direction
			);
		}
	}

	private void BreakWithoutExplosion()
	{
		if (_state == ChalkState.Exploded)
		{
			return;
		}

		_state = ChalkState.Exploded;

		SpawnSceneAtCurrentPosition(BreakVfxScene);

		QueueFree();
	}

	private void SpawnSceneAtCurrentPosition(
		PackedScene scene
	)
	{
		if (scene == null)
		{
			return;
		}

		Node2D instance = scene.Instantiate<Node2D>();

		GetTree().CurrentScene.AddChild(instance);

		instance.GlobalPosition = GlobalPosition;
		instance.Rotation = Rotation;
	}
}
