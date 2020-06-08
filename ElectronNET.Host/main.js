const { app } = require('electron');
const { BrowserWindow } = require('electron');
const path = require('path');
//const cProcess = require('child_process').spawn;
const { runCoreApp } = require('coreclr-hosting');
const portscanner = require('portscanner');
const imageSize = require('image-size');
let io, browserWindows, ipc, coreAppResultPromise, loadURL;
let appApi, menu, dialogApi, notification, tray, webContents;
let globalShortcut, shellApi, screen, clipboard, autoUpdater;
let commandLine, browserView;
let powerMonitor;
let splashScreen, hostHook;
let mainWindowId, nativeTheme;

let manifestJsonFileName = 'electron.manifest.json';
let watchable = false;
if (app.commandLine.hasSwitch('manifest')) {
    manifestJsonFileName = app.commandLine.getSwitchValue('manifest');
};

if (app.commandLine.hasSwitch('watch')) {
    watchable = true;
};

let currentBinPath = path.join(__dirname.replace('app.asar', ''), 'bin');
let manifestJsonFilePath = path.join(currentBinPath, manifestJsonFileName);

// if watch is enabled lets change the path
if (watchable) {
    currentBinPath = path.join(__dirname, '../../'); // go to project directory
    manifestJsonFilePath = path.join(currentBinPath, manifestJsonFileName);
}

const manifestJsonFile = require(manifestJsonFilePath);
if (manifestJsonFile.singleInstance || manifestJsonFile.aspCoreBackendPort) {
    const mainInstance = app.requestSingleInstanceLock();
    app.on('second-instance', () => {
        const windows = BrowserWindow.getAllWindows();
        if (windows.length) {
            if (windows[0].isMinimized()) {
                windows[0].restore();
            }
            windows[0].focus();
        }
    });

    if (!mainInstance) {
        app.quit();
    }
}

app.on('ready', () => {
    if (isSplashScreenEnabled()) {
        startSplashScreen();
    }

    startSocketApiBridge();
});

global.stopCoreApp = function() {
    throw new Error("stopCoreApp has not been set by the implementation! Please call `UseElectron` in your application.");
}

global.wasShutdown = false;
app.on('will-quit', async (event) => {
    if (global.wasShutdown)
        return;
    event.preventDefault();
    try {
    global.stopCoreApp();    
    await coreAppResultPromise;
    } catch(e) {
        console.log("Error during shutdown: " + e)
    }
    global.wasShutdown = true;
    app.quit();
});

function isSplashScreenEnabled() {
    if (manifestJsonFile.hasOwnProperty('splashscreen')) {
        if (manifestJsonFile.splashscreen.hasOwnProperty('imageFile')) {
            return Boolean(manifestJsonFile.splashscreen.imageFile);
        }
    }

    return false;
}

function startSplashScreen() {
    let imageFile = path.join(currentBinPath, manifestJsonFile.splashscreen.imageFile);
    imageSize(imageFile, (error, dimensions) => {
        if (error) {
            console.log(`load splashscreen error:`);
            console.error(error);

            throw new Error(error.message);
        }

        splashScreen = new BrowserWindow({
            width: dimensions.width,
            height: dimensions.height,
            transparent: true,
            center: true,
            frame: false,
            closable: false,
            skipTaskbar: true,
            show: true
        });

        app.once('browser-window-focus', () => {
            app.once('browser-window-focus', () => {
                splashScreen.destroy();
            });
        });

        const loadSplashscreenUrl = path.join(__dirname, 'splashscreen', 'index.html') + '?imgPath=' + imageFile;
        splashScreen.loadURL('file://' + loadSplashscreenUrl);

        splashScreen.once('closed', () => {
            splashScreen = null;
        });
    });
}

function startSocketApiBridge() {
    // prototype
    app['mainWindowURL'] = "";
    app['mainWindow'] = null;

    if (watchable) {
        startAspCoreBackendWithWatch();
    } else {
        startAspCoreBackend();
    }
}

global.initializeElectronNetApi = function (sendToDotNet, registerCallback) {
    console.log("initialize API");
    // Called from dotnet to pass our socket
    const socket = {
        on: function (channel, callback) {
            registerCallback(channel, function (jsonArgs) {
                //console.log("Calling js");
                callback(...JSON.parse(jsonArgs));
            });
        },
        emit: function (channel, ...args) {
            //console.log("Calling dotnet");
            sendToDotNet(channel, JSON.stringify(args));
        }
    }

    appApi = require('./api/app')(socket, app);
    browserWindows = require('./api/browserWindows')(socket, app);
    commandLine = require('./api/commandLine')(socket, app);
    autoUpdater = require('./api/autoUpdater')(socket);
    ipc = require('./api/ipc')(socket);
    menu = require('./api/menu')(socket);
    dialogApi = require('./api/dialog')(socket);
    notification = require('./api/notification')(socket);
    tray = require('./api/tray')(socket);
    webContents = require('./api/webContents')(socket);
    globalShortcut = require('./api/globalShortcut')(socket);
    shellApi = require('./api/shell')(socket);
    screen = require('./api/screen')(socket);
    clipboard = require('./api/clipboard')(socket);
    browserView = require('./api/browserView')(socket);
    powerMonitor = require('./api/powerMonitor')(socket);
    nativeTheme = require('./api/nativeTheme')(socket);

    try {
        const hostHookScriptFilePath = path.join(__dirname, 'ElectronHostHook', 'index.js');

        if (isModuleAvailable(hostHookScriptFilePath) && hostHook === undefined) {
            const { HookService } = require(hostHookScriptFilePath);
            hostHook = new HookService(socket, app);
            hostHook.onHostReady();
        }
    } catch (error) {
        console.error(error.message);
    }

}

function isModuleAvailable(name) {
    try {
        require.resolve(name);
        return true;
    } catch (e) { }
    return false;
}

function startAspCoreBackend() {
    if (manifestJsonFile.aspCoreBackendPort) {
        startBackend(manifestJsonFile.aspCoreBackendPort)
    } else {
        // hostname needs to be localhost, otherwise Windows Firewall will be triggered.
        portscanner.findAPortNotInUse(8000, 65535, 'localhost', function (error, electronWebPort) {
            startBackend(electronWebPort);
        });
    }

    function startBackend(aspCoreBackendPort) {
        console.log('ASP.NET Core Port: ' + aspCoreBackendPort);
        loadURL = `http://localhost:${aspCoreBackendPort}`;
        const parameters = [getEnvironmentParameter(), `/electronWebPort=${aspCoreBackendPort}`];
        let binaryFile = manifestJsonFile.executable + '.dll';
        
        let binFilePath = path.join(currentBinPath, binaryFile);
        
        process.chdir(currentBinPath); // TODO DM 05.06.2020: is this necessary?
        coreAppResultPromise = runCoreApp(binFilePath, ...parameters);        
    }
}

function startAspCoreBackendWithWatch() {
    throw Error("Watch is not supported when hosting the .NET runtime in process. Consider switching to electron-reload for this!");
}

function getEnvironmentParameter() {
    if(manifestJsonFile.environment) {
        return '--environment=' + manifestJsonFile.environment;
    }

    return '';
}