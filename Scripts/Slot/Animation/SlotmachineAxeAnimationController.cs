using MoreMountains.Feedbacks;
using UnityEngine;
using Wooduduk.UI;

namespace Wooduduk.Slot.Machine
{
    public class SlotmachineAxeAnimationController : MonoBehaviour
    {

        private WeaponSkinSwapper _skinSwapper;

        [Header("References")]
        [SerializeField] private Transform _swingDestination;

        private GameObject _axeModel; 
        
        private MMF_Player _axeSwingPlayer;

        private void Awake()
        {

            _skinSwapper = GetComponent<WeaponSkinSwapper>();
            UpdateAxeReference(_skinSwapper.CurrentAxe);
            
        }

        private void OnEnable()
        {
            SlotEvent.OnAxeSwing += PlayAxeSwing;
            SlotEvent.OnSpinCompleted += HideAxe;
            if (_skinSwapper != null)
            {
                _skinSwapper.OnAxeSpawned += UpdateAxeReference;
                if (_skinSwapper.CurrentAxe != null)
                {
                    UpdateAxeReference(_skinSwapper.CurrentAxe);
                }
            }
        }

        private void OnDisable()
        {
            SlotEvent.OnAxeSwing -= PlayAxeSwing;
            SlotEvent.OnSpinCompleted -= HideAxe;

            if (_skinSwapper != null)
            {
                _skinSwapper.OnAxeSpawned -= UpdateAxeReference;
            }
        }
        private void UpdateAxeReference(GameObject newAxe)
        {
            if (newAxe == null) return;

            _axeModel = newAxe;

            _axeSwingPlayer = _axeModel.GetComponentInChildren<MMF_Player>();

            if (_axeSwingPlayer != null && _swingDestination != null)
            {
                var positionFeedback = _axeSwingPlayer.GetFeedbackOfType<MMF_Position>();
                if (positionFeedback != null)
                {
                    positionFeedback.DestinationPositionTransform = _swingDestination;
                }

                var rotationFeedback = _axeSwingPlayer.GetFeedbackOfType<MMF_Rotation>();
                if (rotationFeedback != null)
                {
                    rotationFeedback.ToDestinationTransform = _swingDestination;
                }

            }
            _axeModel.SetActive(false);
        }
        private void PlayAxeSwing()
        {
            if (_axeModel != null)
            {
                _axeModel.SetActive(true);
            }
            
            // Feel 연출 재생
            _axeSwingPlayer?.PlayFeedbacks();
        }

        private void HideAxe()
        {
            // 연출 강제 종료 및 메쉬 숨기기
            _axeSwingPlayer?.StopFeedbacks();
            
            if (_axeModel != null)
            {
                _axeModel.SetActive(false);
            }

            // 도끼의 위치/회전값을 치기 전 초기 상태
            _axeSwingPlayer?.RestoreInitialValues(); 
        }
    }
}