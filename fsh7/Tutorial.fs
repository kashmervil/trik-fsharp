open System
open System.Runtime.InteropServices
open System.Threading
open System.Xml
open System.Diagnostics

module I2C = 
    [<DllImport("libconWrap.so.1.0.0", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)>]
        extern void private wrap_I2c_init(string, int, int)
    [<DllImport("libconWrap.so.1.0.0", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)>]
        extern void private wrap_I2c_SendData(int, int, int) 
    [<DllImport("libconWrap.so.1.0.0", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)>]
        extern int private wrap_I2c_ReceiveData(int reg) 
    let init string deviceId forced = wrap_I2c_init(string, deviceId, forced)
    let send command data len = wrap_I2c_SendData(command, data, len)
    let receive = wrap_I2c_ReceiveData 
    
module Config = 
    type PowerMotorMapping  = { port: string; i2cCommandNumber: int; invert: bool; }
    type AnalogSensorMapping = { port: string; i2cCommandNumber: int }
    type EncoderMapping = { port: string; i2cCommandNumber: int }
    type SensorMapping = { port: string; deviceFile: string; defaultType: string; } 
    
    let mutable private _initScript = ""
    let mutable private _powerMotors = Map.empty
    let mutable private _analogSensors = Map.empty
    
    let analogSensors = _analogSensors
    let powerMotors = _powerMotors
    
    let load (path:string) = 
        let doc = new XmlDocument()
        doc.Load path
        _powerMotors <- 
            (doc.SelectNodes "//config/powerMotors/motor") 
            |> Seq.cast<XmlNode> 
            |> Seq.fold (fun acc node -> 
                acc.Add(node.Attributes.["port"].Value, 
                    { 
                        port = node.Attributes.["port"].Value; 
                        i2cCommandNumber = Convert.ToInt32(node.Attributes.["i2cCommandNumber"].Value, 16);
                        invert = Boolean.Parse(node.Attributes.["invert"].Value);
                    }) ) Map.empty
        _analogSensors <- 
            (doc.SelectNodes "//config/analogSensors/analogSensor") 
            |> Seq.cast<XmlNode> 
            |> Seq.fold (fun acc node -> 
                acc.Add(node.Attributes.["port"].Value, 
                    { 
                        AnalogSensorMapping.port = node.Attributes.["port"].Value; 
                        i2cCommandNumber = Convert.ToInt32(node.Attributes.["i2cCommandNumber"].Value, 16);
                        
                    }) ) Map.empty     
        (doc.SelectSingleNode "//config/initScript").InnerText
        
    let runInitScript (_initScript:string)  = 
        _initScript.Split [| '\n' |]
        |> Array.map (fun s -> s.Trim() )
        |> Array.filter (fun s -> not (s.Length = 0) )
        |> Array.map (fun s -> s.Split([|' '|], 2) )
        |> Array.iter (fun ss ->  Process.Start(ss.[0], ss.[1]).WaitForExit() )
        
[<RequireQualifiedAccess>]
type AnalogRegister = A0 = 0x20 | A1 = 0x21 | A2 = 0x22

let getAnalogSensorRegister = int

let limit l u (x :int) = Math.Min(u, Math.Max (l, x))  

let setPower jack (power:int) =
    let power = limit -100 100 power
    let data = (power &&& 0xFF) <<< 8
    I2C.send Config.powerMotors.[jack].i2cCommandNumber data 1
        
let analogMax = 1024
let analogMin = 0


let AnalogSensor_read number = 
    let value = I2C.receive <| getAnalogSensorRegister number 
                |> limit analogMin analogMax 
    (value - analogMin), (analogMax - analogMin)
   
[<EntryPoint>]
let main argv = 
    Config.load "config.xml" 
    //|> Config.runInitScript
    
    let input name = 
      printf "input %s(%%)=" name
      Int32.Parse(Console.ReadLine())
    let k = input "K"
    let distance = input "Distance"
    
    I2C.init "/dev/i2c-2" 0x48 0
    let stdpow = 0x2A
    
    setPower "2" stdpow
    setPower "4" stdpow
   
    let mutable iter = 0
    let stopwatch = new Stopwatch()
    stopwatch.Start()
    while iter < 1000 do
        let (u, range) = AnalogSensor_read AnalogRegister.A1
        let u' = k * (u - distance * range / 100) / 100 
        let l, r = 50 - u' , 50  + u'
        setPower "2" -l
        setPower "4" r
        printfn "%-10d %6d/%d  l:%6d  r:%d" iter u range l r
        iter <- iter + 1
        
        let skipCount = 20
        if iter % skipCount = 0 then 
            printf "Avg ms per iteration: %d" (stopwatch.ElapsedMilliseconds / int64 skipCount)
        
    setPower "2" 0
    setPower "4" 0
    0

// Справа 15, слева 17