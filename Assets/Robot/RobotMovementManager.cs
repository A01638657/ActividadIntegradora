using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotMovementManager : MonoBehaviour
{
    public Request Request;
    Rigidbody botRigid;
    public float velocity;
    public float tempVelocity;
    public float fixedDistance = 1f;
    public float raySize = 1f;
    public Vector3 hitPoint;
    private Quaternion targetRotation;
    [SerializeField] GameObject center;
    public string whatTouched;
    private float waitSecond;
    public string objectRecognized = "Unknown";
    private PickUpController PUC;
    public int stepsForBot = 0;
    public BotInstantiator BIS;
    public JSONpy toPython;

    Vector3[] directions = new Vector3[] 
    {
        Vector3.right,    // N
        -Vector3.right,   // S
        Vector3.forward, // W
        -Vector3.forward   // E
    };
    string[] directionLabels = new string[] { "N", "S", "W", "E" };

    public (string direction, string objectLabel)[] resultTuple;
    public bool hasObject = false;
    public string ID;

    public string[] objectsRecognized = new string[4];
    public void Init(float _velocity){
        velocity = _velocity;
        tempVelocity = _velocity;
    }

    void Awake(){
        botRigid = GetComponent<Rigidbody>();
        if (Request == null)
        {
            Request = FindObjectOfType<Request>();
        }
        GameObject instantiatorObject = GameObject.Find("Instantiator");
        BIS = instantiatorObject.GetComponent<BotInstantiator>();
        PUC = GetComponent<PickUpController>();
    }
    void Start(){
        ID = GetID(gameObject.name);
        waitSecond = 1 / velocity;
        StartCoroutine(CheckHit());
    }
    void Move(){
        if(velocity != tempVelocity){
            velocity = tempVelocity;
        }
        botRigid.MovePosition(botRigid.position + transform.forward);
    }

    void HitHandler(){
        PerformRaycast();
        Request.SendDataToServer(toPython, HandleServerResponse);
    }

    void HandleServerResponse(string response){
        if(string.IsNullOrEmpty(response)){
            Debug.LogError("Failed to receive a valid response from the server.");
            return;
        }
        switch(response){
            case "move":
                Move();
                break;
            case "grab":
            case "drop":
            case "turn":
                velocity = 0;
                ClampPosition();
                if(response == "turn"){
                    Turn();
                }else if(response == "grab"){
                    Grab();
                }else if(response == "drop"){
                    Drop();
                }
                break;
        }
        
    }
    IEnumerator CheckHit(){
        while(true){
            HitHandler();
            stepsForBot ++;
            yield return new WaitForSeconds(waitSecond);
        }
    }
    void PerformRaycast(){
        Array.Clear(objectsRecognized, 0, objectsRecognized.Length);
        Vector3 p1 = center.transform.position;
        var hitResults = new List<(string direction, string objectLabel)>();

        string rotationDirection = GetRotation();

        for(int i = 0; i < directions.Length; i++){
            Vector3 adjustedDirection = GetAdjustedDirection(directions[i], rotationDirection);
            
            if(Physics.Raycast(p1, center.transform.TransformDirection(adjustedDirection), out RaycastHit hit, raySize)){
                if(hit.collider.gameObject != this.gameObject){
                    whatTouched = hit.collider.gameObject.tag;
                    if(whatTouched == "Bot"){
                        whatTouched = "0";
                    }
                    hitPoint = hit.point;
                    string objectRecognized = hit.collider.gameObject.name; // Use a local variable for recognized object name
                    objectsRecognized[i] = objectRecognized;
                    hitResults.Add((directionLabels[i], whatTouched));
                }
            }else{
                hitResults.Add((directionLabels[i], "0"));
            }
        }

        resultTuple = hitResults.ToArray();
        string res = ConvertResultTupleToString();
        toPython = new JSONpy(ID, GetRotation(), res, hasObject, CompletedScene());

        // Debugging raycasts
        Debug.DrawRay(p1, center.transform.TransformDirection(Vector3.forward) * raySize, Color.red);
        Debug.DrawRay(p1, center.transform.TransformDirection(Vector3.back) * raySize, Color.blue);
        Debug.DrawRay(p1, center.transform.TransformDirection(Vector3.right) * raySize, Color.green);
        Debug.DrawRay(p1, center.transform.TransformDirection(Vector3.left) * raySize, Color.yellow);

    }
    Vector3 GetAdjustedDirection(Vector3 direction, string rotationDirection){
        switch(rotationDirection){
            case "E": //
                return new Vector3(-direction.x, direction.y, -direction.z);
            case "N": //
                return new Vector3(-direction.z, direction.y, direction.x);
            case "W":
                return direction;
            case "S": //
                return new Vector3(direction.z, direction.y, -direction.x);
            default:
                return direction;
        }
    }

    void ClampPosition(){
        Vector3 position = transform.position;

        position.x = Mathf.Round(position.x * 2) / 2f;
        position.z = Mathf.Round(position.z * 2) / 2f;

        if (position.x % 1 == 0){
            position.x += 0.5f;
        }
        if (position.z % 1 == 0){
            position.z += 0.5f;
        }

        transform.position = position;
        botRigid.position = position;
    }
    void ClampRotation(){
        Vector3 eulerRotation = transform.rotation.eulerAngles;
        eulerRotation.y = Mathf.Round(eulerRotation.y / 90f) * 90f;

        eulerRotation.x = 0f;
        eulerRotation.z = 0f;
        transform.rotation = Quaternion.Euler(eulerRotation);
        botRigid.rotation = Quaternion.Euler(eulerRotation);
    }
    
    void Grab(){
        string name = null;
        for(int i = 0; i < objectsRecognized.Length; i++){
            if(objectsRecognized[i] != null){
                if(objectsRecognized[i].StartsWith("Box")||objectsRecognized[i].StartsWith("Book")|| objectsRecognized[i].StartsWith("Kit")){
                    name = objectsRecognized[i];
                    break;
                }
            }
        }if(PUC == null){
            PUC = GetComponent<PickUpController>();
        }
        if(!PUC.hasObject){
            PUC.PickUp(name);
            hasObject = PUC.hasObject;
            ClampPosition();
            ClampRotation();
        }
    }
    void Drop(){
        string name = null;
        for(int i = 0; i < objectsRecognized.Length; i++) {
            if(objectsRecognized[i] != null){
                if(objectsRecognized[i].StartsWith("Rack")){
                    name = objectsRecognized[i];
                    break;
                }
            }
        }
        if(name != null){
            PUC.Drop(name);
            hasObject = PUC.hasObject;
        }else{
            Debug.LogWarning("No object starting with 'Rack' found.");
        }

        ClampPosition();
        ClampRotation();
    }
    
    void Turn(){
        float currentYRotation = transform.eulerAngles.y;
        Quaternion targetRotation = Quaternion.Euler(0, currentYRotation + 90f, 0);

        transform.rotation = targetRotation;
        botRigid.rotation = targetRotation;

        ClampRotation();
        ClampPosition();
    }
    string GetID(string name){
        string[] parts = name.Split(' ');
        return parts[parts.Length - 1];
    }
    string GetRotation(){
        float yRotation = transform.eulerAngles.y;
        yRotation = (yRotation + 360f) % 360f;  // Normalize to 0-360 range

        if (yRotation > 315f || yRotation <= 45f)
            return "W";
        if (yRotation > 45f && yRotation <= 135f)
            return "N";
        if (yRotation > 135f && yRotation <= 225f)
            return "E";
        if (yRotation > 225f && yRotation <= 315f)
            return "S";

        return "Not valid";
    }

    bool CompletedScene(){
        if(BIS.initialPlaced != BIS.totalPlaced){
            return false;
        }
        return true;
    }
    public string ConvertResultTupleToString(){
        if (resultTuple == null || resultTuple.Length == 0)
            return "No results available";
        List<string> formattedResults = new List<string>();

        foreach (var tuple in resultTuple){
            formattedResults.Add($"('{tuple.direction}','{tuple.objectLabel}')");
        }
        return string.Join(", ", formattedResults);
    }

}
