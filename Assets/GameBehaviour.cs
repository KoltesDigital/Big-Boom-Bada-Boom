using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class GameBehaviour : MonoBehaviour {

    public static GameBehaviour instance;

    const int playerCount = 4;
    const int sampleCount = 2048;

    public string microphoneName;
    private int microphoneIndex = 0;

    private AudioSource audioSource;
    private int microphoneMinFrequency;
    private int microphoneMaxFrequency;
    private float[] samples;

    public float helpAngleRange = 0.5f;
    public float helpDistanceRange = 10f;

    public GameObject cameraContainer;
    public GameObject ground;
    public GameObject playerPrefab;

    private PlayerBehaviour[] players;
    public Color[] playerColors;

    public Material groundMaterial;
    public float groundHueFrequency = 1f;
    public float groundHueOffset = 0f;
    public float groundSaturation = 1f;
    public float groundValue = 1f;
    public float groundAlpha = 0.5f;

    public Material backgroundMaterial;

    private bool ended = false;

    public SkinnedMeshRenderer nodCat;
    public Canvas nodCanvas;

    public bool simulatePlayers;

    void OnEnable()
    {
        instance = this;
    }

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        samples = new float[sampleCount];

        UpdateMicrophone();

        players = new PlayerBehaviour[playerCount];
        for (int i = 0; i < playerCount; ++i)
        {
            GameObject playerInstance = Instantiate(playerPrefab) as GameObject;
            PlayerBehaviour player = playerInstance.GetComponent<PlayerBehaviour>();
            player.index = i;
            player.color = playerColors[i];
            players[i] = player;
        }

        StartCoroutine("WaitForRestart");
    }

    public void CheckStatuses()
    {
        foreach (var player in players)
        {
            player.CheckStatus();
        }
    }

    void Restart()
    {
        ended = false;
        nodCat.enabled = false;
        nodCanvas.enabled = false;

        foreach (var player in players)
        {
            player.Restart(ground.transform.position.y);
        }
    }

    public bool HelpDirection(int playerIndex, float playerAngle, out float targetAngle)
    {
        Vector3 playerPosition = players[playerIndex].transform.position;
        int targetIndex = -1;
        float targetDistance = helpDistanceRange;
        targetAngle = 0;

        for (int i = 0; i < playerCount; ++i)
        {
            if (i == playerIndex) continue;

            PlayerBehaviour target = players[i];
            Vector3 diffPosition = target.transform.position - playerPosition;
            float d = diffPosition.magnitude;
            if (d < Mathf.Min(targetDistance, helpDistanceRange))
            {
                float a = Mathf.Atan2(diffPosition.z, diffPosition.x);
                float diffAngle = a - playerAngle;
                while (diffAngle < -Mathf.PI)
                    diffAngle += 2f * Mathf.PI;
                while (diffAngle >= Mathf.PI)
                    diffAngle -= 2f * Mathf.PI;
                if (Mathf.Abs(diffAngle) < helpAngleRange)
                {
                    targetAngle = a;
                    targetDistance = d;
                    targetIndex = i;
                }
            }
        }

        return (targetIndex >= 0);
    }

    void UpdateMicrophone()
    {
        if (audioSource.clip)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }

        microphoneName = Microphone.devices[microphoneIndex].ToString();
        Debug.Log(microphoneName);
        Microphone.GetDeviceCaps(microphoneName, out microphoneMinFrequency, out microphoneMaxFrequency);
        if (microphoneMinFrequency + microphoneMaxFrequency == 0)
            microphoneMaxFrequency = 44100;
    }

    private IEnumerator RefreshAudioClip()
    {
        yield return new WaitForSeconds(5f);
        audioSource.clip = null;
    }

    void Update()
    {
        if (!audioSource.clip)
        {
            audioSource.clip = Microphone.Start(microphoneName, true, 10, microphoneMaxFrequency);
        }
        else if (!audioSource.isPlaying && Microphone.GetPosition(microphoneName) > 0)
        {
            audioSource.Play();
            StartCoroutine("RefreshAudioClip");
        }

        audioSource.GetOutputData(samples, 0);
        float sum = 0;
        for (int i=0; i < sampleCount; ++i)
            sum += samples[i]*samples[i]; // sum squared samples
        float rmsValue = Mathf.Sqrt(sum/sampleCount); // rms = square root of average
        /*
        audioSource.GetSpectrumData(samples, 0, FFTWindow.BlackmanHarris);
        int n1 = (int)Mathf.Floor((lowFrequency * sampleCount) / microphoneMaxFrequency);
        int n2 = (int)Mathf.Ceil((highFrequency * sampleCount) / microphoneMaxFrequency);
        /*float sum = 0;
        // average the volumes of frequencies fLow to fHigh
        for (int i=n1; i<n2; ++i)
            sum += samples[i];
        sum /= (n2 - n1);
        */
        Vector3 position = ground.transform.position;
        //position.x = rmsValue * 10f;
        //position.y = sum * 10f;
        position.y += (rmsValue * 10f - position.y) * (1f - Mathf.Exp(-10f * Time.deltaTime));
        ground.transform.position = position;

        position = cameraContainer.transform.position;
        position.y += (rmsValue * 10f - position.y) * (1f - Mathf.Exp(-1f * Time.deltaTime));
        cameraContainer.transform.position = position;

        if (Input.GetButtonDown("Switch"))
        {
            microphoneIndex++;
            if (microphoneIndex >= Microphone.devices.Length)
                microphoneIndex = 0;
            UpdateMicrophone();
        }

        if (Input.GetButtonDown("Restart"))
        {
            Restart();
        }

        float groundHue = Mathf.Repeat(Time.time * groundHueFrequency, 1f);
        Color color = HSVToRGB(groundHue, groundSaturation, groundValue);
        color.a = groundAlpha;
        groundMaterial.color = color;
        backgroundMaterial.SetFloat("_CenterHue", groundHue + groundHueOffset);
        backgroundMaterial.SetFloat("_CenterSaturation", groundSaturation);
        backgroundMaterial.SetFloat("_CenterValue", groundValue);

        CheckGroundedPlayers();
    }

    void CheckGroundedPlayers()
    {
        if (ended)
            return;

        int groundeds = 0;
        int deads = 0;
        int playerIndex = 0;
        
        for (int i = 0; i < playerCount; ++i)
        {
            var player = players[i];

            switch (player.status)
            {
                case PlayerBehaviour.Status.Unknown:
                    return;

                case PlayerBehaviour.Status.Grounded:
                    ++groundeds;
                    playerIndex = i;
                    break;

                case PlayerBehaviour.Status.Dead:
                    ++deads;
                    break;
            }
        }

        if (groundeds == 1)
        {
            ended = true;
            nodCat.material.color = playerColors[playerIndex];
            nodCat.enabled = true;
            nodCanvas.enabled = true;
            StartCoroutine("SuccessAndRestart");
        }
        else if (deads == playerCount)
        {
            ended = true;
            Restart();
        }
    }

    private IEnumerator SuccessAndRestart()
    {
        yield return new WaitForSeconds(2f);
        Restart();
    }

    private IEnumerator WaitForRestart()
    {
        yield return new WaitForEndOfFrame();
        Restart();
    }

    public static Color HSVToRGB(float H, float S, float V)
 {
         Color col = Color.black;
         float Hval = Mathf.Repeat(H, 1f) * 6f;
         int sel = Mathf.FloorToInt(Hval);
         float mod = Hval - sel;
         float v1 = V * (1f - S);
         float v2 = V * (1f - S * mod);
         float v3 = V * (1f - S * (1f - mod));
         switch (sel + 1)
         {
         case 0:
             col.r = V;
             col.g = v1;
             col.b = v2;
             break;
         case 1:
             col.r = V;
             col.g = v3;
             col.b = v1;
             break;
         case 2:
             col.r = v2;
             col.g = V;
             col.b = v1;
             break;
         case 3:
             col.r = v1;
             col.g = V;
             col.b = v3;
             break;
         case 4:
             col.r = v1;
             col.g = v2;
             col.b = V;
             break;
         case 5:
             col.r = v3;
             col.g = v1;
             col.b = V;
             break;
         case 6:
             col.r = V;
             col.g = v1;
             col.b = v2;
             break;
         case 7:
             col.r = V;
             col.g = v3;
             col.b = v1;
             break;
         }
         col.r = Mathf.Clamp(col.r, 0f, 1f);
         col.g = Mathf.Clamp(col.g, 0f, 1f);
         col.b = Mathf.Clamp(col.b, 0f, 1f);
         return col;
     
 }
}
