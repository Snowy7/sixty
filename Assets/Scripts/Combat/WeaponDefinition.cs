using UnityEngine;

namespace Sixty.Combat
{
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Sixty/Combat/Weapon Definition")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Core")]
        public string weaponName = "Pulse Rifle";
        [Tooltip("Shots fired per second.")]
        public float fireRate = 8f;
        public float damage = 12f;

        [Header("Projectile")]
        public Projectile projectilePrefab;
        public float projectileSpeed = 45f;
        public float projectileLifetime = 2f;
    }
}
