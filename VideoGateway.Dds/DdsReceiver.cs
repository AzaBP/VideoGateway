// El objetivo de este componente es escuchar el
// bus de datos, capturar las muestras dinámicas que simulan 
// la cámara, y transformarlas en un objeto 'VideoFrame'

//Del repositorio se rescata el perfil QoS (USER_QOS_PROFILES.xml)
using Rti.Dds.Core;
using Rti.Dds.Domain;
using Rti.Dds.Subscription;
using Rti.Types.Dynamic;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoGateway.Engine;

namespace VideoGateway.Dds;

public class DdsReceiver
{
    private DomainParticipant? _participant;
    private DataReader<DynamicData>? _reader;
    private bool _isRunning;

    public event EventHandler<VideoFrame>? FrameReceived;

    public void Start(int domainId, string topicName)
    {
        try
        {
            // 1. Crear el participante de forma segura
            _participant = DomainParticipantFactory.Instance.CreateParticipant(domainId);

            // 2. Definición dinámica del tipo adaptada a la especificación estricta de tu API
            var factory = DynamicTypeFactory.Instance;
            var structType = factory.BuildStruct()
                .WithName("VideoData::Frame")
                .AddMember(new StructMember("sequence_number", factory.GetPrimitiveType<long>(), id: 0))
                .AddMember(new StructMember("format", factory.CreateString(), id: 1))
                .AddMember(new StructMember("width", factory.GetPrimitiveType<int>(), id: 2))
                .AddMember(new StructMember("height", factory.GetPrimitiveType<int>(), id: 3))
                .AddMember(new StructMember("frame", factory.CreateSequence(factory.GetPrimitiveType<byte>()), id: 4))
                .Create();

            // 3. Registrar tipo y crear el Topic nativo
            _participant.RegisterType("VideoData::Frame", structType);
            var topic = _participant.CreateTopic(topicName, structType);

            // 4. Configuración del DataReaderQos
            DataReaderQos qos;
            try
            {
                qos = QosProvider.Default.GetDataReaderQos("DataFlowLibrary::Reliable");
            }
            catch
            {
                qos = QosProvider.Default.GetDataReaderQos();
            }

            // 5. Crear el lector de datos dinámicos utilizando el suscriptor implícito
            _reader = _participant.ImplicitSubscriber.CreateDataReader(topic, qos);

            // 6. Arrancar hilo de escucha
            _isRunning = true;
            Task.Run(ListenForFrames);

            Console.WriteLine($"[DDS] Suscrito con éxito al topic '{topicName}' en el dominio {domainId}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DDS] [ERROR] No se pudo inicializar DDS: {ex.Message}");
            throw;
        }
    }

    private void ListenForFrames()
    {
        if (_reader == null) return;

        while (_isRunning)
        {
            try
            {
                using var samples = _reader.Take();

                foreach (var sample in samples)
                {
                    // Validación clásica de muestras de tu versión
                    if (sample.Info.ValidData && sample.Data != null)
                    {
                        DynamicData data = sample.Data;

                        var frame = new VideoFrame
                        {
                            SequenceNumber = Convert.ToInt64(data.GetValue<long>("sequence_number")),
                            Format = data.GetValue<string>("format") ?? "mjpeg",
                            Width = data.GetValue<int>("width"),
                            Height = data.GetValue<int>("height"),
                            Data = data.GetValue<IList<byte>>("frame").ToArray()
                        };

                        FrameReceived?.Invoke(this, frame);
                    }
                }
            }
            catch (Exception ex)
            {
                // Evitamos inundar la consola si no hay datos nuevos en el búfer
                if (!ex.Message.Contains("NO_DATA"))
                {
                    Console.WriteLine($"[DDS] Error leyendo muestras: {ex.Message}");
                }
            }

            System.Threading.Thread.Sleep(10);
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _reader?.Dispose();
        _participant?.Dispose();
        Console.WriteLine("[DDS] Receptor detenido y recursos liberados.");
    }
}