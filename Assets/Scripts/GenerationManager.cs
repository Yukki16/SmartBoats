using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public class GenerationManager : MonoBehaviour
{
    [Header("Generators")] [SerializeField]
    private GenerateObjectsInArea[] boxGenerators;

    [SerializeField] private GenerateObjectsInArea boatGenerator;
    [SerializeField] private GenerateObjectsInArea pirateGenerator;

    [Space(10)] [Header("Parenting and Mutation")] [SerializeField]
    private float mutationFactor;

    [SerializeField] private float mutationChance;
    [SerializeField] private int boatParentSize;
    [SerializeField] private int pirateParentSize;

    [Space(10)] [Header("Simulation Controls")] [SerializeField, Tooltip("Time per simulation (in seconds).")]
    private float simulationTimer;

    [SerializeField, Tooltip("Current time spent on this simulation.")]
    private float simulationCount;

    [SerializeField, Tooltip("Automatically starts the simulation on Play.")]
    private bool runOnStart;

    [SerializeField, Tooltip("Initial count for the simulation. Used for the Prefabs naming.")]
    private int generationCount;

    [Space(10)] [Header("Prefab Saving")] [SerializeField]
    private string savePrefabsAt;

    /// <summary>
    /// Those variables are used mostly for debugging in the inspector.
    /// </summary>
    [Header("Former winners")] [SerializeField]
    private AgentData lastBoatWinnerData;

    [SerializeField] private AgentData lastPirateWinnerData;

    private bool _runningSimulation;
    private List<BoatLogic> _activeBoats;
    private List<PirateLogic> _activePirates;
    private BoatLogic[] _boatParents;
    private PirateLogic[] _pirateParents;


    [SerializeField] float targetPoints = 0;
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
                var pirateParent = lastPirateWinnerData;
                pirate.Birth(pirateParent);
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
                var boatParent = lastBoatWinnerData;
                boat.Birth(boatParent);
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

        var lastBoatWinner = FitnessBasedOnPoints(_boatParents.ToList()).Item1; //Last winner will be the one with the height fitness
        lastBoatWinner.name += "Gen-" + generationCount;
        lastBoatWinnerData = lastBoatWinner.GetData();
        CVSWriter.Instance.WriteCVS(lastBoatWinnerData, CVSWriter.BoatType.Boat, FitnessBasedOnPoints(_boatParents.ToList()).Item2); //Write the winner in the CSV file
        PrefabUtility.SaveAsPrefabAsset(lastBoatWinner.gameObject, savePrefabsAt + lastBoatWinner.name + ".prefab");

        _activePirates.RemoveAll(item => item == null);
        _activePirates.Sort();
        _pirateParents = new PirateLogic[pirateParentSize];
        for (var i = 0; i < pirateParentSize; i++)
        {
            _pirateParents[i] = _activePirates[i];
        }

        var lastPirateWinner = FitnessBasedOnPoints(_pirateParents.ToList()).Item1;
        lastPirateWinner.name += "Gen-" + generationCount;
        lastPirateWinnerData = lastPirateWinner.GetData();
        CVSWriter.Instance.WriteCVS(lastPirateWinnerData, CVSWriter.BoatType.Pirate, FitnessBasedOnPoints(_pirateParents.ToList()).Item2); //Write the winner in the CSV file

        PrefabUtility.SaveAsPrefabAsset(lastPirateWinner.gameObject, savePrefabsAt + lastPirateWinner.name + ".prefab");

        //Winners:
        Debug.Log("Last winner boat had: " + lastBoatWinner.GetPoints() + " points!" + " Last winner pirate had: " +
                  lastPirateWinner.GetPoints() + " points!");

        GenerateObjects(_boatParents, _pirateParents);
    }

    (BoatLogic,float) FitnessBasedOnPoints(List<BoatLogic> _aliveBoats)
    {
        float maxFitness = -1000;
        BoatLogic fittest = new BoatLogic();

        for(int i = 0; i < _aliveBoats.Count; i++)
        {
            if(maxFitness < _aliveBoats[i].GetPoints() / targetPoints)
            {
                maxFitness = _aliveBoats[i].GetPoints() / targetPoints;
                fittest = _aliveBoats[i];
            }
        }

        return (fittest, maxFitness);
    }

    (PirateLogic, float) FitnessBasedOnPoints(List<PirateLogic> _pirates)
    {
        float maxFitness = -1000;
        PirateLogic fittest = new PirateLogic();

        for (int i = 0; i < _pirates.Count; i++)
        {
            if (maxFitness < _pirates[i].GetPoints() / targetPoints)
            {
                maxFitness = _pirates[i].GetPoints() / targetPoints;
                fittest = _pirates[i];
            }
        }

        return (fittest, maxFitness);
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