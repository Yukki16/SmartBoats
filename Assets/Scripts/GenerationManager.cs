using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public class GenerationManager : MonoBehaviour
{
    [Header("Generators")]
    [SerializeField]
    private GenerateObjectsInArea[] boxGenerators;

    [SerializeField] private GenerateObjectsInArea boatGenerator;
    [SerializeField] private GenerateObjectsInArea pirateGenerator;

    [Space(10)]
    [Header("Parenting and Mutation")]
    [SerializeField]
    private float mutationFactor;

    [SerializeField] private float mutationChance;
    [SerializeField] private int boatParentSize;
    [SerializeField] private int pirateParentSize;

    [Space(10)]
    [Header("Simulation Controls")]
    [SerializeField, Tooltip("Time per simulation (in seconds).")]
    private float simulationTimer;

    [SerializeField, Tooltip("Current time spent on this simulation.")]
    private float simulationCount;

    [SerializeField, Tooltip("Automatically starts the simulation on Play.")]
    private bool runOnStart;

    [SerializeField, Tooltip("Initial count for the simulation. Used for the Prefabs naming.")]
    private int generationCount;

    [Space(10)]
    [Header("Prefab Saving")]
    [SerializeField]
    private string savePrefabsAt;

    /// <summary>
    /// Those variables are used mostly for debugging in the inspector.
    /// </summary>
    [Header("Former winners")]
    [SerializeField]
    private AgentData lastBoatWinnerData;

    [SerializeField] private AgentData lastPirateWinnerData;

    private bool _runningSimulation;
    private List<BoatLogic> _activeBoats;
    private List<PirateLogic> _activePirates;
    private BoatLogic[] _boatParents;
    private PirateLogic[] _pirateParents;

    [Serializable]
    private enum FitnessMode
    {
        Points,
        Weights,
        RankedOnPoints,
        RankedOnWeights
    }
    [SerializeField] private FitnessMode _fitnessMode;
    [SerializeField] float targetPoints = 0;
    [SerializeField] float targetWeightsTotal = 0;
    private void Awake()
    {
        Random.InitState(6);
    }

    private void Start()
    {
        if (runOnStart)
        {
            StartSimulation();
        }
    }

    private void Update()
    {
        if (!_runningSimulation) return;
        //Creates a new generation.
        if (simulationCount >= simulationTimer)
        {
            ++generationCount;
            MakeNewGeneration();
            simulationCount = -Time.deltaTime;
        }

        simulationCount += Time.deltaTime;
    }


    /// <summary>
    /// Generates the boxes on all box areas.
    /// </summary>
    public void GenerateBoxes()
    {
        foreach (var generateObjectsInArea in boxGenerators)
        {
            generateObjectsInArea.RegenerateObjects();
        }
    }

    /// <summary>
    /// Generates boats and pirates using the parents list.
    /// If no parents are used, then they are ignored and the boats/pirates are generated using the default prefab
    /// specified in their areas.
    /// </summary>
    /// <param name="boatParents"></param>
    /// <param name="pirateParents"></param>
    public void GenerateObjects(BoatLogic[] boatParents = null, PirateLogic[] pirateParents = null)
    {
        GenerateBoats(boatParents);
        GeneratePirates(pirateParents);
    }

    /// <summary>
    /// Generates the list of pirates using the parents list. The parent list can be null and, if so, it will be ignored.
    /// Newly created pirates will go under mutation (MutationChances and MutationFactor will be applied).
    /// Newly create agents will be Awaken (calling AwakeUp()).
    /// </summary>
    /// <param name="pirateParents"></param>
    private void GeneratePirates(PirateLogic[] pirateParents)
    {
        _activePirates = new List<PirateLogic>();
        var objects = pirateGenerator.RegenerateObjects();
        foreach (var pirate in objects.Select(obj => obj.GetComponent<PirateLogic>()).Where(pirate => pirate != null))
        {
            _activePirates.Add(pirate);
            if (pirateParents != null)
            {
                switch (_fitnessMode)
                {
                    case FitnessMode.Points:
                    case FitnessMode.Weights:
                        var pirateParent = pirateParents[0];
                        pirate.Birth(pirateParents[0].GetData());
                        break;

                    case FitnessMode.RankedOnPoints:
                    case FitnessMode.RankedOnWeights:
                        pirate.Birth(pirateParents[0], pirateParents[1]);
                        break;
                }
            }

            pirate.Mutate(mutationFactor, mutationChance);
            pirate.AwakeUp();
        }
    }

    /// <summary>
    /// Generates the list of boats using the parents list. The parent list can be null and, if so, it will be ignored.
    /// Newly created boats will go under mutation (MutationChances and MutationFactor will be applied).
    /// /// Newly create agents will be Awaken (calling AwakeUp()).
    /// </summary>
    /// <param name="boatParents"></param>
    private void GenerateBoats(BoatLogic[] boatParents)
    {
        _activeBoats = new List<BoatLogic>();
        var objects = boatGenerator.RegenerateObjects();
        foreach (var boat in objects.Select(obj => obj.GetComponent<BoatLogic>()).Where(boat => boat != null))
        {
            _activeBoats.Add(boat);
            if (boatParents != null)
            {
                switch(_fitnessMode)
                {
                    case FitnessMode.Points:
                    case FitnessMode.Weights:
                        var boatParent = boatParents[0];
                        boat.Birth(boatParent.GetData());
                        break;

                    case FitnessMode.RankedOnPoints:
                    case FitnessMode.RankedOnWeights:
                        boat.Birth(boatParents[0], boatParents[1]);
                        break;
                }
                
            }

            boat.Mutate(mutationFactor, mutationChance);
            boat.AwakeUp();
        }
    }

    /// <summary>
    /// Creates a new generation by using GenerateBoxes and GenerateBoats/Pirates.
    /// Previous generations will be removed and the best parents will be selected and used to create the new generation.
    /// The best parents (top 1) of the generation will be stored as a Prefab in the [savePrefabsAt] folder. Their name
    /// will use the [generationCount] as an identifier.
    /// </summary>
    private void MakeNewGeneration()
    {
        Random.InitState(6);

        GenerateBoxes();

        //Fetch parents
        _activeBoats.RemoveAll(item => item == null);
        _activeBoats.Sort();
        if (_activeBoats.Count == 0)
        {
            GenerateBoats(_boatParents);
        }

        _boatParents = new BoatLogic[boatParentSize];
        for (var i = 0; i < boatParentSize; i++)
        {
            _boatParents[i] = _activeBoats[i];
        }

        //var lastBoatWinner = (new BoatLogic(), 0f);
        var _boatFitness = (new List<BoatLogic>(), new List<float>());
        _boatFitness = Fitness(_boatParents.ToList());

        switch (_fitnessMode)
        {
            case FitnessMode.Points:
            case FitnessMode.Weights:
                _boatFitness.Item1[0].name += "Gen-" + generationCount;
                //lastBoatWinnerData = _boatFitness.Item1[0].GetData();
                CVSWriter.Instance.WriteCVS(_boatFitness.Item1[0].GetData(), CVSWriter.BoatType.Boat, _boatFitness.Item2[0]); //Write the winner in the CSV file
                PrefabUtility.SaveAsPrefabAsset(_boatFitness.Item1[0].gameObject, savePrefabsAt + _boatFitness.Item1[0].name + ".prefab");
                break;

            case FitnessMode.RankedOnPoints:
            case FitnessMode.RankedOnWeights:
                _boatFitness.Item1[0].name += "Parent 1 Gen-" + generationCount;
                _boatFitness.Item1[1].name += "Parent 2 Gen-" + generationCount;
                //lastBoatWinnerData = _boatFitness.Item1[0].GetData();
                CVSWriter.Instance.WriteCVS(_boatFitness.Item1[0].GetData(), CVSWriter.BoatType.Boat, _boatFitness.Item2[0]); //Write the winner in the CSV file
                CVSWriter.Instance.WriteCVS(_boatFitness.Item1[1].GetData(), CVSWriter.BoatType.Boat, _boatFitness.Item2[1]); //Write the winner in the CSV file
                PrefabUtility.SaveAsPrefabAsset(_boatFitness.Item1[0].gameObject, savePrefabsAt + _boatFitness.Item1[0].name + ".prefab");
                PrefabUtility.SaveAsPrefabAsset(_boatFitness.Item1[1].gameObject, savePrefabsAt + _boatFitness.Item1[1].name + ".prefab");
                break;
        }


        _activePirates.RemoveAll(item => item == null);
        _activePirates.Sort();
        _pirateParents = new PirateLogic[pirateParentSize];
        for (var i = 0; i < pirateParentSize; i++)
        {
            _pirateParents[i] = _activePirates[i];
        }


        //var lastPirateWinner = (new PirateLogic(), 0f);
        var _pirateFitness = (new List<PirateLogic>(), new List<float>());
        _pirateFitness = Fitness(_pirateParents.ToList());

        switch (_fitnessMode)
        {
            case FitnessMode.Points:
            case FitnessMode.Weights:
                _pirateFitness.Item1[0].name += "Gen-" + generationCount;
                //lastPirateWinnerData = _pirateFitness.Item1[0].GetData();
                CVSWriter.Instance.WriteCVS(_pirateFitness.Item1[0].GetData(), CVSWriter.BoatType.Pirate, _pirateFitness.Item2[0]); //Write the winner in the CSV file

                PrefabUtility.SaveAsPrefabAsset(_pirateFitness.Item1[0].gameObject, savePrefabsAt + _pirateFitness.Item1[0].name + ".prefab");

                //Winners:
                /*Debug.Log("Last winner boat had: " + _boatFitness.Item2[0] + " points!" + " Last winner pirate had: " +
                          _pirateFitness.Item2[0] + " fitness!");*/
                break;

            case FitnessMode.RankedOnPoints:
            case FitnessMode.RankedOnWeights:
                _pirateFitness.Item1[0].name += "Parent 1 Gen-" + generationCount;
                _pirateFitness.Item1[1].name += "Parent 2 Gen-" + generationCount;
                //lastBoatWinnerData = _pirateFitness.Item1[0].GetData();
                CVSWriter.Instance.WriteCVS(_pirateFitness.Item1[0].GetData(), CVSWriter.BoatType.Pirate, _pirateFitness.Item2[0]); //Write the winner in the CSV file
                CVSWriter.Instance.WriteCVS(_pirateFitness.Item1[1].GetData(), CVSWriter.BoatType.Pirate, _boatFitness.Item2[1]); //Write the winner in the CSV file
                PrefabUtility.SaveAsPrefabAsset(_pirateFitness.Item1[0].gameObject, savePrefabsAt + _pirateFitness.Item1[0].name + ".prefab");
                PrefabUtility.SaveAsPrefabAsset(_pirateFitness.Item1[1].gameObject, savePrefabsAt + _pirateFitness.Item1[1].name + ".prefab");
                break;
        }

        GenerateObjects(_boatFitness.Item1.ToArray(), _pirateFitness.Item1.ToArray());
        //GenerateObjects(_boatParents, _pirateParents);
    }

    (List<BoatLogic>, List<float>) Fitness(List<BoatLogic> _boats)
    {
        //float maxFitness = -1000;
        //BoatLogic fittest = new BoatLogic();
        float score = 0;

        //List<float> fitness = new List<float>();
        Dictionary<BoatLogic, float> fitness = new Dictionary<BoatLogic, float>();
        switch (_fitnessMode)
        {
            case FitnessMode.Points:
            case FitnessMode.RankedOnPoints:
                for (int i = 0; i < _boats.Count; i++)
                {
                    fitness.Add(_boats[i], _boats[i].GetPoints() / targetPoints);
                }
                break;

            case FitnessMode.Weights:
            case FitnessMode.RankedOnWeights:
                for (int i = 0; i < _boats.Count; i++)
                {
                    score = _boats[i].GetWeightsTotal();
                    fitness.Add(_boats[i], score / targetWeightsTotal);
                }
                break;

        }
        fitness.OrderBy(kvp => kvp.Value).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return (fitness.Keys.ToList(), fitness.Values.ToList());
    }

    (List<PirateLogic>, List<float>) Fitness(List<PirateLogic> _pirates)
    {
        //float maxFitness = -1000;
        PirateLogic fittest = new PirateLogic();
        float score = 0;

        //List<float> fitness = new List<float>();
        Dictionary<PirateLogic, float> fitness = new Dictionary<PirateLogic, float>();
        switch (_fitnessMode)
        {
            case FitnessMode.Points:
            case FitnessMode.RankedOnPoints:
                for (int i = 0; i < _pirates.Count; i++)
                {
                    fitness.Add(_pirates[i], _pirates[i].GetPoints() / targetPoints);
                }
                break;

            case FitnessMode.Weights:
            case FitnessMode.RankedOnWeights:
                for (int i = 0; i < _pirates.Count; i++)
                {
                    score = _pirates[i].GetWeightsTotal();
                    fitness.Add(_pirates[i], score / targetWeightsTotal);
                }
                break;

        }
        fitness.OrderBy(kvp => kvp.Value).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return (fitness.Keys.ToList(), fitness.Values.ToList());
    }

    /// <summary>
    /// Starts a new simulation. It does not call MakeNewGeneration. It calls both GenerateBoxes and GenerateObjects and
    /// then sets the _runningSimulation flag to true.
    /// </summary>
    public void StartSimulation()
    {
        Random.InitState(6);

        GenerateBoxes();
        GenerateObjects();
        _runningSimulation = true;
    }

    /// <summary>
    /// Continues the simulation. It calls MakeNewGeneration to use the previous state of the simulation and continue it.
    /// It sets the _runningSimulation flag to true.
    /// </summary>
    public void ContinueSimulation()
    {
        MakeNewGeneration();
        _runningSimulation = true;
    }

    /// <summary>
    /// Stops the count for the simulation. It also removes null (Destroyed) boats from the _activeBoats list and sets
    /// all boats and pirates to Sleep.
    /// </summary>
    public void StopSimulation()
    {
        _runningSimulation = false;
        _activeBoats.RemoveAll(item => item == null);
        _activeBoats.ForEach(boat => boat.Sleep());
        _activePirates.ForEach(pirate => pirate.Sleep());
    }
}