using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Wiring")]
    public SceneLoader sceneLoader;      // גרור את אובייקט ה-SceneLoader
    public Button btnOverview;
    public Button btnDeployment;
    public Button btnPackUp;

    [Header("Scene Names (Build Settings)")]
    public string overviewScene = "ViewerScene"; // השם המדויק של הסצנה הקיימת
    public string deploymentScene = "DeploymentScene";
    public string packupScene = "PackUpScene";

    void Start()
    {
        btnOverview.onClick.AddListener(() => sceneLoader.LoadByName(overviewScene));
        btnDeployment.onClick.AddListener(() => sceneLoader.LoadByName(deploymentScene));
        btnPackUp.onClick.AddListener(() => sceneLoader.LoadByName(packupScene));
    }
}