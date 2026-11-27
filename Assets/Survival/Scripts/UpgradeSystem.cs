using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Survival
{
    /// <summary>Between waves: pause and offer 3 random upgrades (click or press 1/2/3).</summary>
    public class UpgradeSystem : MonoBehaviour
    {
        class Upgrade
        {
            #region Variables

            public string title;
            public string description;
            public Action apply;
            public Func<bool> available;

            #endregion
        }

        #region Variables

        public PlayerController player;
        public PlayerHealth playerHealth;
        public GameObject panel;
        public Button[] cards;
        public Text[] titles;
        public Text[] descriptions;

        readonly List<Upgrade> all = new List<Upgrade>();
        readonly Upgrade[] offered = new Upgrade[3];

        #endregion

        #region Properties

        public bool IsOpen { get; private set; }

        #endregion

        #region Unity Event Methods

        void Awake()
        {
            Add("연사 속도", "발사 간격 -18%", () => player.fireInterval *= 0.82f, () => player.fireInterval > 0.04f);
            Add("화력 강화", "총알 데미지 +1", () => player.damage++);
            Add("관통탄", "총알이 적을 1명 더 관통", () => player.pierce++, () => player.pierce < 5);
            Add("산탄", "한 번에 1발 더 발사", () => player.projectiles++, () => player.projectiles < 7);
            Add("신속", "이동 속도 +12%", () => player.moveSpeed *= 1.12f, () => player.moveSpeed < 13f);
            Add("강인함", "최대 체력 +25\n체력 25 회복", () => playerHealth.IncreaseMax(25));
            Add("응급 처치", "체력 60% 회복", () => playerHealth.Heal(Mathf.RoundToInt(playerHealth.maxHealth * 0.6f)),
                () => playerHealth.Current < playerHealth.maxHealth);
            Add("순간 이동", "대시 쿨타임 -25%", () => player.dashCooldown *= 0.75f, () => player.dashCooldown > 0.4f);
            Add("고속탄", "탄속·사거리 +25%", () => player.bulletSpeedMul *= 1.25f, () => player.bulletSpeedMul < 2.5f);
            Add("흡혈", "적 처치 시 체력 +1", () => player.healOnKill++, () => player.healOnKill < 5);

            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                cards[i].onClick.AddListener(() => Choose(index));
            }
            panel.SetActive(false);
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Choose(0);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Choose(1);
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Choose(2);
        }

        #endregion

        #region Custom Methods

        void Add(string title, string description, Action apply, Func<bool> available = null)
        {
            all.Add(new Upgrade { title = title, description = description, apply = apply, available = available ?? (() => true) });
        }

        public void Open()
        {
            var pool = all.FindAll(u => u.available());
            for (int i = 0; i < offered.Length; i++)
            {
                int pick = Random.Range(0, pool.Count);
                offered[i] = pool[pick];
                pool.RemoveAt(pick);
                titles[i].text = offered[i].title;
                descriptions[i].text = offered[i].description;
            }
            panel.SetActive(true);
            IsOpen = true;
        }

        public void Choose(int index)
        {
            if (!IsOpen) return;
            offered[index].apply();
            IsOpen = false;
            panel.SetActive(false);

            var fx = Effects.Instance;
            if (fx != null)
            {
                fx.Burst(player.transform.position + Vector3.up, new Color(1f, 0.85f, 0.3f), 40, 6f, 0.8f);
                fx.FloatText(player.transform.position + Vector3.up * 2.5f, offered[index].title, new Color(1f, 0.85f, 0.3f), 36);
            }
        }

        #endregion
    }
}
