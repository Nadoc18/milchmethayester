using UnityEngine;

namespace MNLTHII.Managers
{
    public class FXManager : MonoBehaviour
    {
        public static FXManager Instance { get; private set; }

        [Header("------- VFX (Assign Prefabs here) ---------")]
        public GameObject spawnCommunityFX;
        public GameObject spawnEnemyFX;
        public GameObject attackFX;
        public GameObject destructionFX;
        public GameObject energyBuffVFX;
        public GameObject crystalBuffVFX; // NOUVEAU : Effet pour le Cristal

        [Tooltip("Impact sur une cible qui survit au coup. Si vide, attackFX est utilise.")]
        public GameObject hitFX;
        [Tooltip("Effet joue quand un batiment monte de niveau. Si vide, spawnCommunityFX est utilise.")]
        public GameObject buildingUpgradeFX;

        [Header("------- Camera Shake ---------")]
        public float hitShakeMagnitude = 1.2f;
        public float hitShakeRoughness = 2.5f;

        [Header("------- Audios (Assign Clips here) ---------")]
        public AudioClip enemySpawnerSFX;
        public AudioClip unitSpawnerSFX;
        public AudioClip attackSFX;

        [Tooltip("Son du deplacement d'une unite. Laisse vide : aucun son ne sort, plutot que d'emprunter celui de l'attaque.")]
        public AudioClip moveSFX;
        public AudioClip explosionSFX;
        public AudioClip buildingSFX;
        
        private AudioSource _audioSource;
        private GameObject _currentFocusInstance;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (Tools.ObjectPooler.Instance != null)
            {
                if (attackFX != null) Tools.ObjectPooler.Instance.CreatePool("AttackFX", attackFX, 10);
                if (destructionFX != null) Tools.ObjectPooler.Instance.CreatePool("DestructionFX", destructionFX, 10);
                if (spawnCommunityFX != null) Tools.ObjectPooler.Instance.CreatePool("SpawnCommunityFX", spawnCommunityFX, 5);
                if (spawnEnemyFX != null) Tools.ObjectPooler.Instance.CreatePool("SpawnEnemyFX", spawnEnemyFX, 5);

                // Les effets de coup et de buff etaient instancies puis detruits a chaque
                // occurrence : ils passent desormais par le pool comme les autres.
                if (hitFX != null) Tools.ObjectPooler.Instance.CreatePool("HitFX", hitFX, 12);
                if (energyBuffVFX != null) Tools.ObjectPooler.Instance.CreatePool("EnergyBuffFX", energyBuffVFX, 8);
                if (crystalBuffVFX != null) Tools.ObjectPooler.Instance.CreatePool("CrystalBuffFX", crystalBuffVFX, 8);
                if (buildingUpgradeFX != null) Tools.ObjectPooler.Instance.CreatePool("BuildingUpgradeFX", buildingUpgradeFX, 5);
            }
        }

        public void PlayBuildingSFX()
        {
            if (buildingSFX != null) { _audioSource.clip = buildingSFX; _audioSource.Play(); }
        }

        public void PlayEnemySpawnerSFX()
        {
            if (enemySpawnerSFX != null) { _audioSource.clip = enemySpawnerSFX; _audioSource.Play(); }
        }

        public void PlayEnemySpawnerSFX(AudioSource source)
        {
            if (enemySpawnerSFX != null && source != null) { source.clip = enemySpawnerSFX; source.Play(); }
        }

        public void PlayUnitSpawnerSFX()
        {
            if (unitSpawnerSFX != null) { _audioSource.clip = unitSpawnerSFX; _audioSource.Play(); }
        }

        public void PlayUnitSpawnerSFX(AudioSource source)
        {
            if (unitSpawnerSFX != null && source != null) { source.clip = unitSpawnerSFX; source.Play(); }
        }

        /// <summary>
        /// Le son du deplacement.
        ///
        /// Il ne retombe PAS sur attackSFX quand il est vide, et c'est delibere : un
        /// bruit d'attaque sur un simple pas apprend au joueur a associer ce son a un
        /// coup porte, et il finit par entendre des attaques qui n'ont pas eu lieu.
        /// Mieux vaut un deplacement silencieux qu'un deplacement qui ment.
        /// </summary>
        public void PlayMoveSFX()
        {
            PlayMoveSFX(null);
        }

        public void PlayMoveSFX(AudioSource source)
        {
            if (moveSFX == null) return;

            AudioSource target = (source != null) ? source : _audioSource;
            if (target != null) target.PlayOneShot(moveSFX);
        }

        public void PlayAttackSFX()
        {
            if (attackSFX != null) { _audioSource.clip = attackSFX; _audioSource.Play(); }
        }
        
        public void PlayAttackSFX(AudioSource source)
        {
            if (attackSFX != null && source != null) { source.clip = attackSFX; source.Play(); }
        }

        public void PlayExplosionSFX()
        {
            if (explosionSFX != null) { _audioSource.clip = explosionSFX; _audioSource.Play(); }
        }
        
        public void PlayExplosionSFX(AudioSource source)
        {
            if (explosionSFX != null && source != null) { source.clip = explosionSFX; source.Play(); }
        }

        public void SpawnAttackFX(Vector3 position)
        {
            if (attackFX == null) return;
            if (Tools.ObjectPooler.Instance != null)
                Tools.ObjectPooler.Instance.SpawnFromPool("AttackFX", position, Quaternion.identity);
            else
                Instantiate(attackFX, position, Quaternion.identity);
        }

        public void SpawnDestructionFX(Vector3 position)
        {
            if (destructionFX == null) return;
            if (Tools.ObjectPooler.Instance != null)
                Tools.ObjectPooler.Instance.SpawnFromPool("DestructionFX", position, Quaternion.identity);
            else
                Instantiate(destructionFX, position, Quaternion.identity);
        }

        public void SpawnCommunityFX(Vector3 position)
        {
            if (spawnCommunityFX == null) return;
            if (Tools.ObjectPooler.Instance != null)
                Tools.ObjectPooler.Instance.SpawnFromPool("SpawnCommunityFX", position, Quaternion.identity);
            else
                Instantiate(spawnCommunityFX, position, Quaternion.identity);
        }

        public void SpawnEnemyFX(Vector3 position)
        {
            if (spawnEnemyFX == null) return;
            if (Tools.ObjectPooler.Instance != null)
                Tools.ObjectPooler.Instance.SpawnFromPool("SpawnEnemyFX", position, Quaternion.identity);
            else
                Instantiate(spawnEnemyFX, position, Quaternion.identity);
        }

        public void SpawnEnergyBuffFX(Vector3 position)
        {
            SpawnPooled("EnergyBuffFX", energyBuffVFX, position);
        }

        public void SpawnCrystalBuffFX(Vector3 position)
        {
            SpawnPooled("CrystalBuffFX", crystalBuffVFX, position);
        }

        // =================================================================
        //  IMPACT SUR UNE CIBLE QUI SURVIT
        // =================================================================
        /// <summary>
        /// Joue l'impact sur une unite ou une structure touchee mais pas detruite :
        /// particule, son d'impact et petite secousse de camera.
        /// </summary>
        public void SpawnHitFX(Vector3 position)
        {
            GameObject prefab = (hitFX != null) ? hitFX : attackFX;
            string poolKey = (hitFX != null) ? "HitFX" : "AttackFX";

            SpawnPooled(poolKey, prefab, position);
            PlayAttackSFX();
            ShakeLight();
        }

        /// <summary>Secousse courte, pour un coup encaisse (les destructions en ont une plus forte).</summary>
        public void ShakeLight()
        {
            if (EZCameraShake.CameraShaker.Instance == null) return;
            EZCameraShake.CameraShaker.Instance.ShakeOnce(hitShakeMagnitude, hitShakeRoughness, 0.05f, 0.4f);
        }

        /// <summary>Effet de construction / montee de niveau d'un batiment.</summary>
        public void SpawnBuildingUpgradeFX(Vector3 position)
        {
            if (buildingUpgradeFX != null) SpawnPooled("BuildingUpgradeFX", buildingUpgradeFX, position);
            else SpawnCommunityFX(position);

            PlayBuildingSFX();
        }

        /// <summary>
        /// Clignotement de substitution entre l'ancien et le nouveau modele, puis
        /// destruction de l'ancien. Delegue au VFXManager historique de la scene.
        /// </summary>
        public void PlayEvolutionFX(GameObject newAsset, GameObject assetToDestroy)
        {
            if (VFXManager.instance != null && newAsset != null)
                VFXManager.instance.EvolutionFX(newAsset, assetToDestroy);
            else if (assetToDestroy != null)
                Destroy(assetToDestroy);
        }

        private void SpawnPooled(string poolKey, GameObject prefab, Vector3 position)
        {
            if (prefab == null) return;

            if (Tools.ObjectPooler.Instance != null)
            {
                GameObject spawned = Tools.ObjectPooler.Instance.SpawnFromPool(poolKey, position, Quaternion.identity);
                if (spawned != null) return;
            }

            GameObject go = Instantiate(prefab, position, Quaternion.identity);
            Destroy(go, 2.0f);
        }

        /// <summary>
        /// PLUS PERSONNE N'APPELLE CECI, ET C'EST VOULU.
        ///
        /// Le trait de laser ne s'affichait pas en jeu - vraisemblablement un
        /// LineRenderer dont le materiau n'a pas de shader valide dans le pipeline
        /// integre. Les deux appels, cote Bunker et cote Yetzer Hara, ont ete retires
        /// plutot que rafistoles : la lecture d'un tir passe mieux par la camera qui
        /// cadre le duel, l'impact sur la cible et le chiffre de degats.
        ///
        /// La methode reste ici pour le jour ou LaserFX sera repare. Avant de la
        /// rebrancher, verifie qu'elle dessine vraiment quelque chose a l'ecran.
        /// </summary>
        public void SpawnLaser(Vector3 start, Vector3 end, bool isEnemy)
        {
            SpawnLaser(start, end, isEnemy ? Color.red : new Color(0f, 1f, 1f, 1f), 0.5f);
        }

        public void SpawnLaser(Vector3 start, Vector3 end, Color color, float duration)
        {
            GameObject laserObj = new GameObject("LaserBeam");
            LaserFX laser = laserObj.AddComponent<LaserFX>();
            laser.ShootLaser(start, end, color, duration);
        }

        public void SetUnitFocus(Transform targetTransform, bool isEnemy = false)
        {
            if (targetTransform == null)
            {
                if (_currentFocusInstance != null)
                {
                    _currentFocusInstance.SetActive(false);
                }

                // La phase est finie : la camera revient a sa place.
                CameraDirector.ReleaseCamera();
                return;
            }

            if (_currentFocusInstance == null)
            {
                _currentFocusInstance = new GameObject("FocusSpotlight");
                Light light = _currentFocusInstance.AddComponent<Light>();
                light.type = LightType.Spot;
                light.intensity = 8f; // Restored intensity
                light.range = 15f;
                light.spotAngle = 45f;
                _currentFocusInstance.AddComponent<FocusBouncer>();
            }

            Light l = _currentFocusInstance.GetComponent<Light>();
            if (l != null)
            {
                if (isEnemy)
                {
                    l.color = new Color(1.0f, 0.3f, 0.0f); // Orange/Red
                }
                else
                {
                    l.color = new Color(0.1f, 0.6f, 1.0f); // Blue
                }
            }

            _currentFocusInstance.SetActive(true);
            _currentFocusInstance.transform.SetParent(targetTransform);
            _currentFocusInstance.transform.localPosition = new Vector3(0, 4f, 0);
            _currentFocusInstance.transform.localRotation = Quaternion.Euler(90, 0, 0);

            // Le projecteur DESIGNE l'unite ; la camera, elle, rapproche le regard.
            // Les deux passent par ce seul point d'entree, deja appele par la boucle
            // des Tanks et par celle du Yetzer Hara : un seul branchement couvre les
            // deux phases, et il ne peut pas y en avoir une qui oublie l'autre.
            CameraDirector.Focus(targetTransform);
        }
    }
}