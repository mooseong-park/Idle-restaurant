using UnityEngine;

namespace Project.Visual
{
    // 캐릭터 이동 + bob/sway. POCO — Customer/Staff 양쪽이 보유.
    //
    // 가속/감속 (v²/2a 정지거리 기반) + 누적 이동 거리 기반 bob (속도와 자연스럽게 동기).
    // abs(sin) hop 패턴 (위로만 튀는 점프) + 절반 주파수 sway (펜듈럼).
    // Idle mode: target 도달 후 정지 상태에서 호흡 같은 미세 bob.
    public sealed class CharacterMover
    {
        // 튜닝 — Customer/Staff 인스턴스 별로 직접 설정 (B1 단계에서 SO 외부화 예정).
        public float MaxMoveSpeed = 1f;
        public float Acceleration = 2.5f;
        public float StepsPerUnit = 1.3f;
        public float BobAmpY = 0.20f;
        public float SwayAmpX = 0.02f;
        public float IdleBobAmp = 0.04f;
        public float IdleBobFreq = 1.6f;
        public float ArriveEpsilon = 0.04f;

        public Vector3 MovePos { get; private set; }
        public float CurrentSpeed { get; private set; }
        public float WalkDistance { get; private set; }

        float idleBobTimer;

        public void Reset(Vector3 startPos)
        {
            MovePos = startPos;
            CurrentSpeed = 0f;
            WalkDistance = 0f;
            idleBobTimer = 0f;
        }

        // target 으로 이동 + bob/sway 적용된 최종 위치 반환. enableIdleBob: 정지 시 호흡 bob 활성화.
        public Vector3 Tick(float dt, Vector3 target, bool enableIdleBob)
        {
            Vector3 delta = target - MovePos;
            float dist = delta.magnitude;

            if (dist > ArriveEpsilon)
            {
                // 정지 거리: v²/(2a). 남은 거리가 그 이내면 감속, 아니면 가속.
                float stopDist = CurrentSpeed * CurrentSpeed / (2f * Acceleration);
                float targetSpeed = dist <= stopDist ? 0f : MaxMoveSpeed;
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, Acceleration * dt);

                float step = CurrentSpeed * dt;
                if (step >= dist)
                {
                    MovePos = target;
                    CurrentSpeed = 0f;
                }
                else
                {
                    MovePos += delta * (step / dist);
                    WalkDistance += step;
                }
                idleBobTimer = 0f;
            }
            else
            {
                MovePos = target;
                CurrentSpeed = 0f;
                if (enableIdleBob) idleBobTimer += dt * IdleBobFreq * Mathf.PI * 2f;
                else idleBobTimer = 0f;
            }

            // bob/sway 합성
            Vector3 finalPos = MovePos;
            if (CurrentSpeed > 0.05f)
            {
                float phase = WalkDistance * StepsPerUnit * Mathf.PI * 2f;
                float intensity = Mathf.Clamp01(CurrentSpeed / MaxMoveSpeed);
                finalPos.y += Mathf.Abs(Mathf.Sin(phase)) * BobAmpY * intensity;
                finalPos.x += Mathf.Sin(phase * 0.5f) * SwayAmpX * intensity;
            }
            else if (enableIdleBob)
            {
                finalPos.y += Mathf.Sin(idleBobTimer) * IdleBobAmp;
            }
            return finalPos;
        }
    }
}
