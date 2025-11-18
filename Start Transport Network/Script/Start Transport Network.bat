echo off

echo "Starting Transport Network"

echo "*****************************************************************************"
echo "* REBUILD IS NOT AUTOMATED, PLEASE VERIFY THAT YOU ARE RUNNING CURRENT CODE *"
echo "*****************************************************************************"

::pause

echo "Starting Hubs"

cd "C:\Users\Admin\source\repos\Transport Toolkit\Hub\bin\Debug\net8.0"

echo "Starting East Hub"

start "East Hub" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\East Hub" "Hub.exe""

echo "East Hub Started"

echo "Starting West Hub"

start "West Hub" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\West Hub" "Hub.exe""

echo "West Hub Started"

echo "Starting Middle Hub"

start "Middle Hub" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\Middle Hub" Hub.exe
   
echo "Middle Hub Started"

echo "Hubs Started"

echo "Starting Relays"

cd "C:\Users\Admin\source\repos\Transport Toolkit\Relay\bin\Debug\net8.0"

echo "Starting East Middle Relay"

start "East Middle Relay" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\East Middle Relay" Relay.exe

echo "East Middle Relay Started"

echo "Starting Middle East Relay"

start "Middle East Relay" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\Middle East Relay" Relay.exe

echo "Middle East Relay Started"

echo "Starting West Middle Relay"

start "West Middle Relay" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\West Middle Relay" Relay.exe

echo "West Middle Relay Started"

echo "Starting Middle West Relay"

start "Middle West Relay" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\Middle West Relay" Relay.exe

echo "Middle West Relay Started"

echo "Relays Started"

echo "Starting Reflectors"

cd "C:\Users\Admin\source\repos\Transport Toolkit\Reflector\bin\Debug\net8.0"

echo "Starting East Inbound Reflector"

start "East Inbound Reflector" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\East Inbound Reflector" Reflector.exe

echo "East Inbound Reflector Started"

echo "Starting East Outbound Reflector"

start "East Outbound Reflector" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\East Outbound Reflector" Reflector.exe

echo "East Outbound Reflector Started"

echo "Starting West Inbound Reflector"

start "West Inbound Reflector" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\West Inbound Reflector" Reflector.exe

echo "West Inbound Reflector Started"

echo "Starting West Outbound Reflector"

start "West Outbound Reflector" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\West Outbound Reflector" Reflector.exe

echo "West Outbound Reflector Started"

echo "Reflectors Started"

echo "Transport Network Started"

echo "Starting Receivers"

cd "C:\Users\Admin\source\repos\Transport Toolkit\Receiver\bin\Debug\net8.0"

echo "Starting East Receiver"

::start "East Receiver" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\East Receiver" Receiver.exe
   
echo "East Receiver Started"

echo "Starting West Receiver"

::start "West Receiver" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\West Receiver" Receiver.exe
   
echo "West Receiver Started"

echo "Receivers Started"

echo "Starting Transmitters"

cd "C:\Users\Admin\source\repos\Transport Toolkit\Transmitter\bin\Debug\net8.0"

echo "Starting East Transmitter"

::start "East Transmitter" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\East Transmitter" Transmitter.exe
   
echo "East Transmitter Started"

echo "Starting West Transmitter"

::start "West Transmitter" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\West Transmitter" Transmitter.exe
   
echo "West Transmitter Started"

echo "Transmitters Started"

echo "Starting Clients"

cd "C:\Users\Admin\source\repos\Transport Toolkit\Client\bin\Debug\net8.0"

echo "Starting East Client"

start "East Client" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\East Client" Client.exe
   
echo "East Client Started"

echo "Starting West Client"

start "West Client" /d "C:\Users\Admin\source\repos\Transport Toolkit\Start Transport Network\West Client" Client.exe
   
echo "West Client Started"

echo "Clients Started"

pause