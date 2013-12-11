module Config

open System
open System.Xml
open System.Diagnostics
    type PowerMotorMapping  = { port: string; i2cCommandNumber: int; invert: bool; }
    type AnalogSensorMapping = { port: string; i2cCommandNumber: int }
    type EncoderMapping = { port: string; i2cCommandNumber: int }
    type SensorMapping = { port: string; deviceFile: string; defaultType: string; } 

type Config (path:string) =

    let doc = new XmlDocument()
    do       doc.Load path
    let powerMotors =
        (doc.SelectNodes "//config/powerMotors/motor") 
        |> Seq.cast<XmlNode>
        |> Seq.map (fun node -> 
            (node.Attributes.["port"].Value, 
                { 
                    port = node.Attributes.["port"].Value; 
                    i2cCommandNumber = Convert.ToInt32(node.Attributes.["i2cCommandNumber"].Value, 16);
                    invert = Boolean.Parse(node.Attributes.["invert"].Value);
                }) )
        |> dict
    let analogSensors = 
        (doc.SelectNodes "//config/analogSensors/analogSensor") 
        |> Seq.cast<XmlNode>
        |> Seq.map (fun node -> 
            (node.Attributes.["port"].Value, 
                { 
                    AnalogSensorMapping.port = node.Attributes.["port"].Value; 
                    i2cCommandNumber = Convert.ToInt32(node.Attributes.["i2cCommandNumber"].Value, 16);
                }) )
        |> dict
    
    do
      (doc.SelectSingleNode "//config/initScript").InnerText.Split [| '\n' |]
        |> Array.map (fun s -> s.Trim() )
        |> Array.filter (fun s -> not (s.Length = 0) )
        |> Array.map (fun s -> s.Split([|' '|], 2) )
        |> ignore
        //|> Array.iter (fun ss ->  Process.Start(ss.[0], ss.[1]).WaitForExit() )
        
    member x.PowerMotor = powerMotors
    member x.analogSensor = analogSensors