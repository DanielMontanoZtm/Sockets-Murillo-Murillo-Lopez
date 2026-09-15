using System;
using System.Net.Sockets;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SmartphoneSocketController : MonoBehaviour
{
    [Header("Connection")]
    [SerializeField] private TMP_InputField ipField;
    [SerializeField] private TMP_InputField portField;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private float sendInterval = 0.05f;

    [Header("Controls")]
    [SerializeField] private Slider moveXSlider;
    [SerializeField] private Slider moveZSlider;
    [SerializeField] private Slider yawSlider;

    [Header("Usability")]
    [SerializeField] private bool autoCenterSliders = true;

    private TcpClient client;
    private NetworkStream stream;
    private float nextSendTime;
    private bool grabRequested;
    private bool releaseRequested;

    private void Start()
    {
        if (autoCenterSliders)
        {
            SetupAutoCenter(moveXSlider);
            SetupAutoCenter(moveZSlider);
            SetupAutoCenter(yawSlider);
        }
    }

    private void SetupAutoCenter(Slider slider)
    {
        if (slider == null) return;

        EventTrigger trigger = slider.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = slider.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry pointerUpEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerUp
        };
        pointerUpEntry.callback.AddListener((_) =>
        {
            slider.value = 0f;
        });
        trigger.triggers.Add(pointerUpEntry);
    }

    public async void Connect()
    {
        CloseSocketOnly();

        if (ipField == null || string.IsNullOrWhiteSpace(ipField.text))
        {
            SetStatus("Error: Ingrese la IP del PC");
            return;
        }

        string ip = ipField.text.Trim();
        int port = 7777;
        if (portField != null && !string.IsNullOrWhiteSpace(portField.text))
        {
            if (!int.TryParse(portField.text.Trim(), out port))
            {
                SetStatus("Error: El puerto debe ser numérico");
                return;
            }
        }

        SetStatus($"Conectando a {ip}:{port}...");

        try
        {
            client = new TcpClient();
            var connectTask = client.ConnectAsync(ip, port);
            var timeoutTask = System.Threading.Tasks.Task.Delay(4000);

            var completedTask = await System.Threading.Tasks.Task.WhenAny(connectTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                CloseSocketOnly();
                SetStatus("Error: Tiempo agotado. Verifique la IP o el Firewall del PC");
                return;
            }

            await connectTask;

            if (client.Connected)
            {
                stream = client.GetStream();
                SetStatus($"¡Conectado a {ip}:{port}!");
            }
            else
            {
                CloseSocketOnly();
                SetStatus("Error: No se pudo establecer la conexión");
            }
        }
        catch (Exception ex)
        {
            CloseSocketOnly();
            SetStatus($"Error: {ex.Message}");
        }
    }

    private void CloseSocketOnly()
    {
        try
        {
            stream?.Close();
            client?.Close();
        }
        catch (Exception)
        {
            // Ignorar excepciones al cerrar
        }
        finally
        {
            stream = null;
            client = null;
        }
    }

    public void Disconnect()
    {
        CloseSocketOnly();
        SetStatus("Desconectado");
    }

    private void Update()
    {
        if (stream == null || Time.time < nextSendTime)
            return;

        SendCurrentInput();
        nextSendTime = Time.time + sendInterval;
    }

    private void SendCurrentInput()
    {
        if (stream == null || !client.Connected)
            return;

        RemoteControlMessage message = new RemoteControlMessage();

        message.x = moveXSlider != null ? moveXSlider.value : 0f;
        message.z = moveZSlider != null ? moveZSlider.value : 0f;
        message.yaw = yawSlider != null ? yawSlider.value : 0f;
        message.grab = grabRequested;
        message.release = releaseRequested;

        string json = JsonUtility.ToJson(message) + "\n";
        byte[] data = Encoding.UTF8.GetBytes(json);

        try
        {
            stream.Write(data, 0, data.Length);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error enviando datos: {ex.Message}");
            Disconnect();
            SetStatus("Conexión perdida con el servidor");
            return;
        }

        grabRequested = false;
        releaseRequested = false;
    }

    public void RequestGrab()
    {
        grabRequested = true;
    }

    public void RequestRelease()
    {
        releaseRequested = true;
    }


    private void OnApplicationQuit()
    {
        Disconnect();
    }

    private void SetStatus(string message)
    {
        Debug.Log(message);

        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}