using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class enemy : MonoBehaviour
{
    float maxHP;
    float HP;
    bool dead;
    enemy()
    {
        HP = 8;
        maxHP = 8;
    }
    enemy(float health)
    {
        HP = health;
        maxHP = health;
    }
    private void Start()
    {
        dead = false;
    }
    public void Dmg()
    {
        HP--;
        GameManager._Instance.UpdateEnemyUIdmg(gameObject);
        if (HP < 1 && !dead)
        {
            dead = true;
            GameManager._Instance.Enemykill(gameObject, true);
            Destroy(gameObject);
        }
    }
    public void Dmg(float damage)
    {
        HP -= damage;
        if (HP < 1) GameManager._Instance.Enemykill(gameObject,true);
    }
    public float Hpercent()
    {
        return HP / maxHP;
    }    
}
