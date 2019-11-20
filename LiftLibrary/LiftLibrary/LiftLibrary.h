/******************************************************************************/
/**
* @file LiftLibrary.h
* @brief Funktionen und Framework für die Steuerung des LiftSimulators
* @author Werner Odermatt
* @author Rolf Laich
*
* Im Unterricht der Module M121 sowie M242 wird ein Liftsimulator verwendet, welcher auf dem ATMEL chipset ATMEGA32 basiert. 
* Dieser Simulator bildet einen Lift mit vier Etagen ab. Die Library liefert die folgenden Funktionen:
* - Eine Infrastruktur für die Implementierung und Registrierung von Zustandsmaschinen
* - Eine Infrastruktur für das Versenden von Messages
* - Einbindung der seriellen Schnittstelle für Debugging, Test und Remote User Interface, welches auf dem PC implementiert ist.
* - Hardware-Abstraktionen für die Liftsimulation
* 
* @date 25.05.2015 Erste Implemenierung W.Odermatt
* @date 07.09.2016 Ueberarbeitung der Software, Code-Richtlinien C#
* @date 07.25.2019 Verwendung der UART für 
*       - Debugging und test
*       - User-Interaktionen; 
*       - Lift-Buttons/Etage-Buttons (PortD ist für UART gebraucht)
* @date 10.10.2019 Einführung eines einfachen State-Event frameworks 
*       - Lift und Türen bewegen läuft auf Timer-Interrupt
*       - INT1 Trigger für Türstopp
*       - Funktionen für Atomare Code Abschnitte
*       - Kapselung von avr/IO.h
* @date 22.10.2019 Formatierung der Kommentare und Dokumenation mit Doxygen
* @date 19.11.2019 Regelmässige Status Updates für Terminal-Applikation
* 
* @version 3.0
*
*/

#include <inttypes.h>				// 

/******************************************************************************/
/*** EIGENE DATENTYPEN ********************************************************/
/******************************************************************************/

/** 
* @brief enumerate für die explizite Festlegung von false und true
*
* In neueren Versionen des C-Compilers sind true und false bereits definiert.
*/
typedef enum
{
	false = 0,
	true = 1	
}Boolean;

 typedef enum 
 {
	 PacketType_Undefined = 0,
	 PacketType_TraceMessage = 1,
	 PacketType_LiftSimulatorButton = 2,
	 PacketType_TestCommand = 3,
	 PacketType_LiftStatus = 4
 }AvrPacketType;
 

/**
* @brief Beschreibt die eine Etage im System
*
* Der FloorType wird sowohl für die Anzeige als auch für die bestimmung der Position des Liftes verwendet.
*/
typedef enum 
{
  Floor0  = 0, Floor1 = 1, Floor2  = 2, Floor3  = 3
}
FloorType;

/**
* @brief Type mit Bitmaske für die verschiedenen Buttons
*/
typedef enum 
{ 
  EmergencyButton = 0,
	LiftButton_F0 = 1, 
	LiftButton_F1 = 2, 
	LiftButton_F2 = 4,
	LiftButton_F3 = 8,
	FloorButton_F0 = 16, 
	FloorButton_F1 = 32,
  FloorButton_F2 = 64, 
  FloorButton_F3 = 128
}
ButtonType;

/**
* @brief Bitmaske für den Zustand der Tür      
*/
typedef enum 
{
  Door100 = 0xF0,	//< 100% geschlossen
  Door75 = 0x70,	//< 75% geschlossen
  Door50 = 0x30,	//< 50% geschlossen
  Door25 = 0x10,	//< 25% geschlossen
  Door00 = 0x00		//< offen
}
DoorPosType;

/**
* @brief Beschreibt den Zustand der Tür
*/
typedef enum 
{
  Open = 0x10, 
  Opening = 0x11,
  Closed = 0x20,
  Closing = 0x21,
}
DoorStateType;

/**
* @brief Beschreibt den Zustand der Stockwerksanzeige
*/
typedef enum 
{
  On, Off
}
DisplayStateType;

/**
* @brief Beschreibt den Zustand eines Buttons, welches das Event
* ausgelöst hat.
*/
typedef enum 
{
  Released, Pressed
}
ButtonStateType;

/** 
* @brief Beschreibt den aktuellen Zustand der Fahrgastzelle
*/
typedef enum 
{
  LiftStateNone = 0, 
  LiftStateMoves = 0x10, 
  LiftStateError = 0x20,
  LiftStateOverload =  0x40,
  LiftStateUpperStop = 0x80,
  
}
LiftStateType;

/** 
* @brief Spezifizert die Fahrtrichtung des Liftes
*/
typedef enum 
{
	Down = -1, 
	DirectionNone = 0,
	Up = 1
}
DirectionType;

/**
* @brief Spezifiziert die Geschwindigkeit des Lifts
*
* Der Speedtype erlaubt es, Geschwindigkeitsrampen zu implementieren, wenn der Lift über grössere Distanzen
* fährt                                                       
*/
typedef enum 
{
	Slow = 8, Medium = 6, Fast = 4, VeryFast = 2
}
SpeedType;


/** 
* @brief Beschreibt die möglichen Ereignisquellen des Systems
*
* Jede ereignisquelle wird durch ein separates Bit modelliert. Dadurch ist es möglich, sich auf einfache Art und Weise
* für eine oder mehrere Signalquellen zu registrieren.
*/
typedef enum
{
	SignalSourceEnvironment =	0x01,
	SignalSourceElevator =		0x02, 
	SignalSourceEtageButton =	0x04, 
	SignalSourceLiftButton =	0x08,
	SignalSourceDoor =			0x10,  
}
SignalSourceType;

/** 
* @brief Beschreibt die Meldungs-Id's welche vom Framework generiert werden
*
* Das Framework gibt einige Medlungen vor. Alle *Systemmeldungen* haben eine Id welche > 192 ist. Meldungen, welche in der 
* eigentlichen Applikation definiert werden, sollen eine kleinere Id haben.
*/
typedef enum
{
	SystemMessage = 0xC0,
	LiftStarted,
	LiftCalibrated,
	EnterTestMode,
	ExitTestMode,
	LiftDoorClosed,
	LiftDoorOpen,
	ElevatorAtFloor,
	DoorEmergencyBreak,
	
}WellKnownMessageIds;

/**
* @brief Item um Information zwischen verschiedenen Komponenten des Systems auszutauschen.
*
* 
*/
typedef struct Message_tag
{
	uint8_t Source;					///< Identification des Erzeugers (Sender) der Meldung (a SignalSource)
	uint8_t Id;						///< Id der Meldung
	uint8_t MsgParamLow;			///< lower byte[0] des Meldungs-Parameters
	uint8_t MsgParamHigh;			///< upper byte[1] des Meldungs-Paramsters 
}Message;


/** 
* @brief typedef um status information an den Terminal zu schicken
*
* Diese Information wird benötigt, um die Buttons auf dem Bildschirm zu enabled/disablen
*/
typedef struct LiftStatus_tag
{	
	uint8_t SystemStatus;
	uint8_t OpenDoors;
} LiftStatus;



/**
* @brief Prototyp einer Zustandsfunktion
*/
 typedef void (*StateHandler)( Message* msg);

/**
* @brief Callback-Funktion, welche über einen Positionswechsel der Kabine informiert.
* @param currentPosition enthält die aktuelle Position der Fahrgastzelle
* @param targetPosition enthält die Zielposition der Fahrgastzelle
*
* Dieser Callback informiert den Prozess in definierten Zeitabschnitten über die aktuelle Position. 
* Damit kann z.B. die Positionsanzeige gesteuert werden. 
* Die Funktion wird im Interrutp-Kontext des Timers aufgerufen. z.Z. gibt es keine Zeitüberwachung für ISR's.
*
*/
typedef void (*PositionChangeSignal)(uint8_t currentPosition, uint8_t targetPosition);

/**
* @brief Repräsentiert eine Zustandsmaschine
*
* Eine oder mehrere Zustandsmaschinen können im Framework registriert werden. Beim Auftreten einer Meldung werden alle
* Zustandsmaschinen, welche auf diese *EventSource* registriert sind, notifiziert
*/
typedef struct Fsm_tag
{
	struct Fsm_tag* Next;			///< nächste registrierte Zustanndsmaschine
	uint8_t RxMask;					///< Maske, welche angibt, welche *EventSources* bearbeitet werden sollen
	StateHandler CurrentState;		///< Funktion, welche den aktuellen Zustand representiert
}Fsm;


typedef void (*TestHandlerCallback)(uint8_t* payload, uint8_t len );

/******************************************************************************/
/*** GLOBALE KONSTANTEN *******************************************************/
/******************************************************************************/

// SYSTEMEINSTELLUNGEN
// Taktfrequenz des Controllers
//#define F_CPU		8000000UL	// 8 MHz



/** Anzahl der vorhanden Tueren */
#define maxDoors	4

/** hoechster Positionswert des Liftes */
#define liftMaxPos	49   


/******************************************************************************/
/*** GLOBALE FUNKTIONEN *******************************************************/
/******************************************************************************/


/**
* @brief Markiert den Beginn einer atomaren Ausführung
*/
void EnterAtomic(void);

/**
* @brief Markiert das Ende einer atomaren Ausführung
*/
void LeaveAtomic(void);

/**
* @brief Funktion für das Ausführen eine *Zustandübergangs*
*
* @param fsm Zuststandsmaschine, welche den Zustandsübergang ausführen soll
* @param state Funktion, welche den neuen Zustand implementiert
*/
void SetState( Fsm* fsm, StateHandler state );

/**
* @brief Registriert eine Zustandsmaschine im Framework
* @param fsm Zustandsmaschine, welche registriert werden soll 
*/
void RegisterFsm( Fsm* fsm);

/** @brief Funktion zum Senden einer Meldung
*
* @param source Id des Senders der Meldung
* @param id Id der Meldung
* @param msgLow low byte des Meldungs-Parameters
* @param msgHigh upper byte des Meldungs-Parameters
*/
void SendEvent(uint8_t source, uint8_t id, uint8_t msgLow, uint8_t msgHigh);


/** 
* @brief Registrierung einer TestHandler-Funktion, welche aufgerufen wird, um einen Test-Befehl vom PC zu bearbeiten
*
* @param testHandler Befehls-Interpreter
*/
void RegisterTestHandler( TestHandlerCallback testHandler );

/** 
* @brief Initialisierug der notwendigen Ports für das Framework          
*/
void InitializePorts(void);

/**
* @brief Setzen des Anfangszustandes (I/O); Public-Function
*/
void InitializeStart(void);

/**
* @brief Initialisierung der UART Schnittstelle
*
* Die Serielle Schnittstelle wird verwendet um mit dem PC zu kommunizieren.
* Die Schnittstelle wird auf 38400 bauds initialisiert mit 8 Daten-Bits, einem Stop-Bit, ohne Parity-Bit
* RX ist auf Port D0 gemapped, TX auf Port D1
* Das entspricht den Pins 1 und 2 auf dem Simulationsboard     
*/
void Usart_Init(void);

/** 
* @brief Schreibens eines Zeichens auf die serielle Schnittstelle.
* @param ch Zeichen, welches ausgegeben werden soll.
*/
void Usart_PutChar(char ch);

/**
* @brief Kalibrieren der Fahrgastzelle auf die Position: Etage0
*/
void CalibrateElevatorPosition(PositionChangeSignal notify);


// Lesen des Zustandes einer bestimmten Taste; Public-Function
ButtonStateType ReadKeyEvent (ButtonType button);

// Lesen des Zustandes der Lifttuere einer Etage; Public-Function
DoorStateType ReadDoorState (FloorType floor);

// Setzen des Tuerenzustandes pro Etage; Public-Function
void SetDoorState (DoorStateType state, FloorType floor);

/// Setzen der Geschwindigkeit der Fahrgastzelle
void SetElevatorSpeed(SpeedType speed);

/** 
* @brief Startet die Fahrt der Fahrgastzelle
*
* @param targetPos Zielposition der Fahrgastzelle
* @param signal Callback über welchen die aktuelle Position mitgegeben wird 
*/
void MoveElevator(uint8_t targetPos, PositionChangeSignal signal);

/**
* @brief Liefert den aktuellen Zustand des Lifts
*
*/
LiftStateType ReadElevatorState();


/** 
* @brief Setzen der numerischen Etagenanzeige im Lift
*
* Die numerische Etagenanzeige ist die (7-Segment-Anzeige) auf dem Simulationsboard
*/
void SetDisplay (FloorType displayValue);

/** 
* @brief Setzen der Ruftastenanzeige pro Etage
*
* Wird benötigt, um anzuzeigen, dass jemand auf einer bestimmten Etage am Warten ist!
*/
void SetIndicatorFloorState (FloorType floor);


/**
* @brief Setzen der Etagenauswahlanzeige im Lift
*
* Wird benötigt, um anzuzeigen, wohin jemand fahren will!
*/
void SetIndicatorElevatorState (FloorType floor);

/** 
* @brief Loeschen der Ruftastenanzeige pro Etage; Public-Function
*/
void ClrIndicatorFloorState (FloorType floor);

/** 
* @brief Loeschen der Etagenauswahlanzeige im Lift; Public-Function
*/
void ClrIndicatorElevatorState (FloorType floor);

/** 
* @brief Bearbeiten von Meldungen, welche über UART empfangen wurden; Public-Function
*
* 
*/
void ProcessMessage(uint8_t msgType, uint8_t* msg, uint8_t msgLen);