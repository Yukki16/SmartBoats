using TMPro;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PirateLogic : AgentLogic
{
    #region Static Variables

    private static float _boxPoints = 0.1f;
    private static float _boatPoints = 5.0f;

    #endregion

    public float GetWeightsTotal()
    {
        return boatWeight + boxWeight + enemyWeight;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!other.gameObject.tag.Equals("Box")) return;
        points += _boxPoints;
        Destroy(other.gameObject);
    }

    private void OnCollisionEnter(Collision other)
    {
        if (!other.gameObject.tag.Equals("Boat")) return;
        points += _boatPoints;
        Destroy(other.gameObject);
    }
}