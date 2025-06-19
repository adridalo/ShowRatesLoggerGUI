# ShowRatesLoggerGUI | Adriano D'Alonzo

This application is used to log rates from a Mura video wall to a file in which you connect to, and specify the time in seconds of how often these rates are logged. Can be used to view how rates are doing over a longer period of time. You can specify whether you'd like to

1. Retrieve the overall average of all sources up on the wall, which results in a smaller text file or;
2. Retrieve the rates of EACH sources as well as the average, but will result in a much larger file.

## How to use

1. From within the application folder, find the '_ShowRatesLoggerGUI.exe' executable and run it (a pop-up will appear asking if you would like to run it (not recognized by Windows so it will deem it as harmful software). Click 'Run' and open the application).
2. Upon opening of the app, you'll be asked to enter an IP address of the wall you'd like to interact with. Enter the IP of the system you'd like to get rates for and click 'Connect'.

Note: Make sure that the system you're interacting with is on the same network as the system running the app (your PC), or app will not be happy.

3. If it connects successfully, you'll then be asked to enter the interval in which you'd like rates to be collected. Input is in seconds. 
4. By default, the application will just collect the average rates of all sources. If you'd like to collect the rates for each source, check on "Show rates on each source" to get all rates' values.
5. Click run once ready. Once the first rate call was collected, a 'Open File' button will appear enabled, and you can view the log as its happening in real time. Click it to open the file. At any time should you want to stop the application, click the 'Stop' button. The resulting text file will be located in your Desktop.

Note: For whatever interval you enter, you'll have to wait x amount of seconds before being able to view the text file.

***

## Upcoming features for the foreseeable future:
- ~~For overall average of sources, show the number of sources~~
- ~~Add CSV implementation so if user would like to sort lowest rates, it would be easier to see when rates were to have gone bad.~~
- ~~Add timer for how long to run the logging for (Thanks Ben). Make time more inclusive (hours/minutes/seconds)~~
- ~~Identify log files by time so you can have multiple logs of rates from the same IP (right now existing logs get deleted on each run).~~
- ~~Add current rates to GUI (Jason)~~
- ~~Set parameter on each value for when it goes below the value, send notification (somehow) (Lucas)~~
- - Visualise rates in a chart (ScottPlot)

## Nice to have features (not necessary):
- Theme changing (dark/light) (NTH)
- Custom file path location (NTH)
  
***

For any questions and/or feature implementations, contact me @ adalonzo@matrox.com
