using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class CVSWriter : MonoBehaviour
{
    public static CVSWriter Instance;

    string boatFileName;

    string pirateFileName;
    public enum BoatType
    {
        Boat,
        Pirate
    }
    // Start is called before the first frame update
    void Start()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
        boatFileName = Application.dataPath + "/Boat.csv";
        pirateFileName = Application.dataPath + "/PirateResults.csv";
        TextWriter tw = new StreamWriter(boatFileName, true);
        tw.WriteLine("Steps, RayRadius, Sight, MovingSpeed, RandomDirectionValue, Weights, boxWeight, distanceFactor, boatWeight, boatDistanceFactor, enemyWeight, enemyDistanceFactor, Fitness");
        tw.Close();

        tw = new StreamWriter(pirateFileName, true);
        tw.WriteLine("Steps, RayRadius, Sight, MovingSpeed, RandomDirectionValue, Weights, boxWeight, distanceFactor, boatWeight, boatDistanceFactor, enemyWeight, enemyDistanceFactor, Fitness");
        tw.Close();
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void WriteCVS(AgentData agentData, BoatType boatType, float fitness)
    {
        TextWriter tw;
        if (boatType == BoatType.Boat)
        {
            tw = new StreamWriter(boatFileName, true);
        }
        else
        {   
            tw = new StreamWriter(pirateFileName, true);
        }

        tw.WriteLine($"{agentData.steps}, {agentData.rayRadius}, {agentData.sight}, {agentData.movingSpeed}, X:{agentData.randomDirectionValue.x} Y:" +
                $"{agentData.randomDirectionValue.y},,{agentData.boatWeight}, {agentData.distanceFactor},{agentData.boatWeight}, {agentData.boatDistanceFactor}," +
                $"{agentData.enemyWeight}, {agentData.enemyDistanceFactor},  {fitness}");
        tw.Close();
    }
}
