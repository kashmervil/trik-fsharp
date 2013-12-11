module Config

open System
open System.Xml
open System.Diagnostics
//open Mono.Unix.Native
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
    let Syscall_system cmd  = 
        let args = sprintf "-c '%s'" cmd
        printf "%s" cmd
        let proc = Process.Start("/bin/sh", args)
        proc.WaitForExit()
        if proc.ExitCode  <> 0 then
            printf "Init script failed at '%s'" cmd
        else ()
        printfn " Done"
  
    do
        (doc.SelectSingleNode "//config/initScript").InnerText.Split([| '\n' |])
                        |> Seq.filter (fun s -> s.Trim() <> "")
                        |> Seq.iter Syscall_system
      
    member x.PowerMotor = powerMotors
    member x.analogSensor = analogSensors