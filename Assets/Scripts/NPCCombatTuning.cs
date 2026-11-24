using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class NPCCombatTuning : MonoBehaviour
{
	[SerializeField, Min(0f)] private float timeToAim = 3f;
	[SerializeField, Min(0f)] private float timeBetweenAttacks = 0.01f;

	[Header("Weapon")]
	[SerializeField] private GunType weapon = GunType.SemiAutoBlaster;
	[SerializeField, Tooltip("The selector that creates this NPC's weapon. If left empty, the first selector in this hierarchy is used.")]
	private GunSelector gunSelector;

	public float TimeToAim => timeToAim;
	public float TimeBetweenAttacks => timeBetweenAttacks;
	public GunType Weapon => weapon;

	private void Awake()
	{
		if (gunSelector == null)
		{
			gunSelector = GetComponentInChildren<GunSelector>(true);
		}

		if (gunSelector == null)
		{
			Debug.LogError($"{nameof(NPCCombatTuning)} on '{name}' could not find a {nameof(GunSelector)} to configure.", this);
			return;
		}

		gunSelector.ConfigurePrimaryWeapon(weapon);
	}
}
