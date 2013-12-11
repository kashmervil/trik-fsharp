open System
open System.Diagnostics  
open System.Threading  
open Config        
        
[<RequireQualifiedAccess>]
type AnalogRegister = A0 = 0x20 | A1 = 0x21 | A2 = 0x22

let getAnalogSensorRegister = int

let limit l u (x :int) = Math.Min(u, Math.Max (l, x))  

let config = new Config("config.xml")

let setPower jack (power:int) =
    let power = limit -100 100 power
    let data = (power &&& 0xFF)
    I2C.send config.PowerMotor.[jack].i2cCommandNumber data 1
    //printfn "motor: %A" config.PowerMotor.[jack].i2cCommandNumber
let analogMax = 1024
let analogMin = 0


let AnalogSensor_read number = 
    let value = I2C.receive <| getAnalogSensorRegister number 
                |> limit analogMin analogMax 
    (value - analogMin), (analogMax - analogMin)
   
[<EntryPoint>]
let main argv = 
    let input name d = 
      printf "input %s(%%%d)=" name d
      let status, res = Int32.TryParse(Console.ReadLine())
      if status then res else d
      
    let k = input "K" 80
    let initialPow = input "InitialPow" 50

    Thread.Sleep(300);

    I2C.init "/dev/i2c-2" 0x48 1
    let stdpow = 0x2A
    let motorL = "4"
    let motorR = "1"
    AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> setPower motorL 0; setPower motorR 0)
    
    setPower motorL stdpow
    setPower motorR stdpow
   
    let readDistance () = AnalogSensor_read AnalogRegister.A1
    
    
    let distance  = 
       Seq.init 100 <| fun _ -> readDistance() |> fst |> float
       |> Seq.average
       |> int
    
    printfn "Measured distance (units) = %d" distance
    let mutable iter = 0
    let stopwatch = Stopwatch.StartNew()
    while iter < 300 do
        let (u, range) = readDistance ()
        let u' = k * (u - distance) / range
        let l, r = initialPow + u' , initialPow  - u'
        
        setPower motorL -l
        setPower motorR -r
        
        if iter % 2 = 0 then  
            printfn "%-10d %6d/%d  l:%6d  r:%d" iter u range l r
        else ()
        iter <- iter + 1
        
        let skipCount = 20
        if iter % skipCount = 0 then 
            printfn "Avg ms per iteration: %d" (stopwatch.ElapsedMilliseconds / int64 skipCount)
            stopwatch.Reset()
            stopwatch.Start()   
    0

// Справа 15, слева 17