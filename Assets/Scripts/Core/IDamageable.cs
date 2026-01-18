using UnityEngine;

/* 📋 LOGIC MEMO: IDamageable
--------------------------------------------------
1. Core: Universal contract for anything that can die.
--------------------------------------------------
*/
public interface IDamageable
{
    void TakeDamage(int amount);
}
