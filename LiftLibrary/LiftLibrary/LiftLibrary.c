/*******************************************************************************
* Programm:  Bibliotheksfunktionen fuer Liftsteuerung
* Filename:   LiftLibraryWOd_V2.c
*
* Autor:     Werner Odermatt; Rolf Laich
* Version:   3.0
* Datum:	 23.09.2019
*
* Entwicklungsablauf(Version, Datum, Autor, Entwicklungsschritt, Zeit):
* 1.0   25.05.15   WOd   Funktion: initalizePorts() erstellt
* 2.0   07.09.16   WOd   Ueberarbeitung der Software, Code-Richtlinien C#
* 3.0   10.10.19   RLa   Ueberarbeitung der Library:
*                        -> Hardware Simulationen laufen auf Timer-Interrupt
*                        -> Verwendung der UART für Debugging und Test
*                        -> Verwendung der UART für User-Interaktionen; Lift-Buttons/Etage-Buttons (PortD ist für UART gebraucht)
*                        -> INT1 Trigger für Türstopp / Emergency Button
*                        -> Funktionen für Atomare Code Abschnitte
*                        -> einfaches Framework für Zustandsmaschinen und Events
*                        -> Kapselung von avr/IO.h
********************************************************************************
*
* Verwendungszweck: C-Schulung, M121, M242
*
* Beschreibung:
* Dieses Programm simuliert einen Lift mit 4 Etagen
*
* .
*
* Precondition:  -
*
* Postcondition: -
*
* Benoetigte Libraries:
* - avr/io.h
* -
*
* Erstellte Funktionen:
* - initializePort()
* Copyright (c) 2016 by W.Odermatt, R. Laich
*******************************************************************************/


/***  Include Files ***********************************************************/
#include <avr/io.h>
#include <avr/interrupt.h>
#include <inttypes.h>
#include "LiftLibrary.h"

#define USART_RX_BUFFER_SIZE 32
#define MSG_Q_SIZE 32
#define DOOR_OPENCLOSE_SPEED  12

char USART_rxBuffer[USART_RX_BUFFER_SIZE];
static volatile uint8_t USART_rxBufferIn = 0;
static volatile uint8_t USART_rxBufferOut = 0; 

static uint8_t UsedTimers;
static uint16_t WaitingTimer[8];

/******************************************************************************/
/*** Lokale Macros ********************************************************/
/******************************************************************************/
#define TX_ENABLE = (1<<TXEN)




/** PORT- und PINZUWEISUNGEN **/
// PORT A:
#define liftDisplay_D     DDRA  // 7-Seg.; Etagenauswahlanz.; Ruftastenanz.
#define liftDisplay       PORTA // 7-Seg.: Pin 0-2; Etagenauswahlanz.: Pin 3-4
// Ruftastenanz.: Pin 5-7

// PORT B:
#define liftPos_D         DDRB  // liftPosition: Pin 0-5
#define liftPos           PORTB // Tuersimulation LED:1_10: Pin 6; Low = ON
// Etagenauswahlanzeige: Pin 7; Low = ON

// PORT C
#define liftDoors_D       DDRC  // Tuersimulation: Pin 0-3; Etage; High = ON
#define liftDoors         PORTC // Pin 4: LED:5_6; Pin 5: LED:4_7
// Pin 6: LED:3_8; Pin 7: LED:2_9

// PORT D:
#define buttons_D         DDRD  // Etagenauswahltasten im Lift: Pin 0-3
#define buttons           ButtonState  // Ruftasten pro Etage: Pin 4-7


/******************************************************************************/
/*** GLOBALE VARIABLEN *******************************************************/
/******************************************************************************/



/******************************************************************************/
/*** EIGENE DATENTYPEN ********************************************************/
/******************************************************************************/
typedef struct {
  DoorStateType	state;
  int8_t		position;
} DoorType;


/******************************************************************************/
/*** PRIVATE VARIABLEN ********************************************************/
/******************************************************************************/
static DisplayStateType  liftDisplay7Seg_On;  // Zustand der 7-Seg. Anzeige (ON-OFF)
static FloorType         liftDisplay7Seg;	  // Stockwerkanzeige im Lift (7-Segmentanz.)
static uint8_t           floorDisplayPort;    // Uebermittelter Wert an Port

static DisplayStateType  buttonLiftLed_on;    // Zustand der Tastenanzeige im Lift (On-OFF)
static FloorType         buttonLiftLed;       // Anzeige Etagenauswahl im Lift

static DisplayStateType  buttonFloorLed_On;   // Zustand der Ruftastenanzeige (ON-OFF)
static FloorType         buttonFloorLed;      // Anzeige der Ruftaste pro Etage

static uint8_t           displayCache;        // Schattenspeicher der Anzeige (LED)

static DisplayStateType  liftPosDisplay_On;   // Zustand der Luftpositionsanzeige
static uint8_t           liftPosition;        // Position des Liftes
static uint8_t           liftPositionPort;    // Uebermittelter Wert an Port

static DisplayStateType  doorframe;           // LED: 1,10 der Lifttueren
static TestHandlerCallback _testHandler;		  // test kommand interpreter


DoorType          liftDoorState[maxDoors];   // Speichert den Zustand der einzelnen Tueren ab

DoorPosType       doorPositions[5] = {Door00, Door25, Door50, Door75, Door100};


volatile uint16_t ButtonState  = 0;  // das kann im Interrupt-Kontext geschrieben werden
volatile uint8_t SystemState  = 0;  // das kann im Interrupt-Kontext geschrieben werden
static uint8_t OpenDoors = 0;
Boolean EnableStatusUpdate = false;


typedef struct ElevatorType_tag
{
	uint8_t			Position;
	uint8_t			Target;
	SpeedType		Speed;
	LiftStateType   Status;
	DirectionType   Direction;
	PositionChangeSignal OnPositionChanged;
}ElevatorType;

static uint8_t enterAtomicNesting = 0;


static ElevatorType Elevator = {.Position=31, .Direction=Down, .Speed=Fast, .Status=LiftStateNone };
	
static Fsm anchor = {.CurrentState = 0, .Next = &anchor , .RxMask = 0};

static Message msgQueue[MSG_Q_SIZE];
static uint8_t msgQ_in = 0;
static uint8_t msgQ_out = 0;

/*******************************************************************************
***  Funktions-Deklarationen ***************************************************
*******************************************************************************/
// Tueren in den angegebenen Zustand bringen; Private-Function
void MakeDoorStates(void);


// Ansteuerung der Ausgabeports
void SetOutput();

/*******************************************************************************
********************************************************************************
*** PRIVATE FUNKTIONEN *********************************************************
********************************************************************************
*******************************************************************************/


/*******************************************************************************
* Zustand der Lifttuere einer Etage in den vorgegebenen Zustand bringen
*******************************************************************************/
void MakeDoorStates (void)
{
	for(uint8_t floor = 0; floor < 4; floor++ )
	{
		if( liftDoorState[floor].position == 0 )
		{
			if( liftDoorState[floor].state & DoorMooving)
			{
				liftDoorState[floor].state &= ~DoorMooving;
				SendEvent(SignalSourceDoor, LiftDoorEvent, liftDoorState[floor].state, floor);	
				if( liftDoorState[floor].state & DoorOpen)
				{
					OpenDoors |= (1<<floor);
				}
				else
				{
					OpenDoors &= ~(1<<floor);
				}
			}
			
		}
		else
		{
			liftDoorState[floor].position --;
		}
	}
}


/*******************************************************************************
********************************************************************************
*** GLOBALE FUNKTIONEN *********************************************************
********************************************************************************
*******************************************************************************/

void EnterAtomic(void)
{
	cli(); // this just forces the bit to be cleared; should be possible to call this many times without side effect
	enterAtomicNesting ++;
}

void LeaveAtomic(void)
{
	enterAtomicNesting--;
	if(enterAtomicNesting == 0)
	{
		sei();
	}	
}

void SetState(Fsm* fsm, StateHandler handler)
{
	EnterAtomic();
	fsm->CurrentState = handler; // we never do this in interrupt context!
	LeaveAtomic();
}

/********************************************************************************
* register a new finite state machine;
* this function will never be called from ISR context; hence no need to protect
********************************************************************************/
void RegisterFsm(Fsm* fsm)
{
	//Usart_PutChar(0xDD);
	Fsm *p = &anchor;	
	Fsm *q = p->Next;
	while(q!=&anchor)
	{
		p = q;
		q = q->Next;
	}
	p->Next = fsm;
	fsm->Next = q;
	//Usart_PutChar(0xDF);
}

/*******************************************************************************
* send a message to all fsm's with a matching rxMask
********************************************************************************/


void SendEvent(uint8_t mask, uint8_t id,  uint8_t msgLow, uint8_t msgHigh)
{
	EnterAtomic();
	if( ((msgQ_in + 1)%MSG_Q_SIZE) == msgQ_out )
	{
		// what shall we do in this case?
		// dump message and stay here forever??
		Usart_PutChar(0xFF);
		Usart_PutChar(0x00);
		Usart_PutChar(0xFF);
		while(1)
		{
			Usart_PutChar(0xDE);
			Usart_PutChar(0xAD);
		}
	}
	msgQueue[msgQ_in].Id = id;
	msgQueue[msgQ_in].MsgParamHigh = msgHigh;
	msgQueue[msgQ_in].MsgParamLow = msgLow;
	msgQueue[msgQ_in].Source = mask;
	msgQ_in++;
	msgQ_in %= MSG_Q_SIZE;
	LeaveAtomic();
}

void DispatchEvent(void)
{
	Message *msg = 0;
	EnterAtomic();
	if( msgQ_out != msgQ_in) // there are waiting messages in the queue
	{
		msg = &msgQueue[msgQ_out++];
		msgQ_out %= MSG_Q_SIZE;
	}
	LeaveAtomic();
	if ( msg != 0 )
	{
		Fsm *p = anchor.Next;
		while( p != &anchor )
		{
			if( (p->RxMask & msg->Source) != 0 )
			{
				p->CurrentState(msg);
			}
			p = p->Next;
			
		}
	}
}



/*******************************************************************************
* Initialisierung der Ports
*******************************************************************************/
void InitializePorts(){
  // Setzt die DDR-Ports der Liftsteuerungssimulation auf die richrtigen Werte
  liftPos_D     = 0xFF;
  liftDoors_D   = 0xFF;
  liftDisplay_D = 0xFF;
  buttons_D     = 0x00; //set all to input; the initialisation of USART will overwrite what is neccessary
  Usart_Init();
  MCUCR |= 3;		// PortD2 is mapped to INT0; this will trigger on rising endge
  GICR |= (1<<6);	// enable INT0
}

void InitializeCounter()
{
	
	TCNT0 = 0;
	TCCR0 = 0x0B;		//prescaler = 5 (1024), wgm12=1 (0x8 =Clear timer on counter match)
	OCR0 = 8000/64;
	TIMSK |=0x2;		// enable compare match irq
		
	OCR1A = 0x180;		// compare register
	TCNT1 = 0;			// initialize the counter to 0
	TCCR1A = 0x00;  
	TCCR1B = 0x0D;		//prescaler = 5, wgm12=1 (0x8)
	TIMSK |= 0x10;		//enable interrupt
}




/**
* @brief Initialisierung des Anfangszustandes
*
* Die Funktion InitializeStart() started das Framework.
*/
void InitializeStart(){
  
  Usart_PutChar(0xCA);
  
  // Aktivierung der Liftpositionsanzeige
  liftPosDisplay_On = On;

  // Aktivierung der Etagenauswahlanzeige (Anzeige im Lift)
  buttonLiftLed_on = On;

  // Aktivierung der Etagenanzeige im Lift (7-Segment-Anzeige)
  liftDisplay7Seg_On = On;

  // Aktivierung der Ruftastenanzeige (Anzeige auf jeder Etage)
  buttonFloorLed_On = On;

  // Aktivierung des Tuerrahmens
  doorframe = On;

  // Alle Lifttueren schliessen
  for (int8_t count = Floor0; count <= Floor3; count++){
    liftDoorState[count].position = 0;
    liftDoorState[count].state = DoorClosed;
  }

  // Setzt den Lift auf einen bestimmten Positionswert
  liftPosition = 31;

  
  InitializeCounter();
  SendEvent( SignalSourceEnvironment, LiftStarted, 0, 0 );
  Usart_PutChar(0xFE);
  
  while(1)
  {
	  DispatchEvent();	  
	  SetOutput();
  }
  
}


/*******************************************************************************
* Kalibrieren der Fahrgastzelle auf die Position: Etage0
*******************************************************************************/
void CalibrateElevatorPosition(PositionChangeSignal notify)
{  
	MoveElevator(0, notify);
}

void HandleMessage(char receivedData)
{
	//Usart_PutChar(0xaa);
	//Usart_PutChar(receivedData);
	static uint8_t msgBuffer[14]; // longest
	static uint8_t bufferIndex = 0;
	static uint8_t msgType = 0;
	static uint8_t msgLen = 0;
	if ( msgType == 0)
	{
		msgType = receivedData;
		msgLen = 0;
	}
	else if(msgLen == 0)
	{
		msgLen = receivedData-2;
		
		bufferIndex = 0;
	}
	else if(bufferIndex < msgLen)
	{
		msgBuffer[bufferIndex++] = receivedData;
		if( bufferIndex == msgLen )
		{
			ProcessMessage(msgType,  msgBuffer, msgLen );
			msgType = 0;
			bufferIndex = 0;
		}
	}
}

void ProcessMessage(uint8_t msgType, uint8_t* msg, uint8_t msgLen)
{
	if( msgType == PacketType_LiftSimulatorButton)
	{
		//Usart_PutChar(0xab);
		uint8_t receivedData = msg[0];
		if( ((receivedData&0x40) != 0) || ((receivedData&0x20) != 0))
		{
			//Usart_PutChar(0x12);
			
			uint8_t buttonState = (receivedData & 0x10)==0x10; // pressed or released
			uint8_t shiftOffset = 0;
			
			//Usart_PutChar(buttonState);
			
			if( receivedData & 0x20 )
			{
				shiftOffset = 4;
			}
			//Usart_PutChar(0x13);
			uint8_t shift = (receivedData&0xF) + shiftOffset;
			//Usart_PutChar(shift);
			ButtonState &= ~(1<<shift); // clear the bit if set
			if(buttonState)
			{
				ButtonState |= (1<<shift);
			}
				
			SendEvent(SignalSourceEnvironment, ButtonEvent, (1<<shift), buttonState);
		}
	
		//Usart_PutChar(0x14);	
		//Usart_PutChar(ButtonState);
	}
	else if( msgType == PacketType_TestCommand)
	{
		if( _testHandler != 0 )
		{
			_testHandler(msg, msgLen);
		}
	}
}

/*******************************************************************************
* Ansteuerung der Ausgabeports
*******************************************************************************/
void SetOutput(){
  
  static uint16_t outputRefreshCounter = 0;
  static uint8_t doorRefreshCounter = 0;
  static uint8_t buttonRefreshCounter = 0;
  static uint16_t terminalRefreshCounter = 0;
  
  //lokale Variablen
  
  DisplayStateType buttonLiftLed_on_tmp;
  DisplayStateType buttonFloorLed_On_tmp;

  outputRefreshCounter++;
  if (outputRefreshCounter % 4 == 0 )
  {
	  doorRefreshCounter++;
	  uint8_t refreshingFloor= doorRefreshCounter%4;
	  uint8_t position = liftDoorState[refreshingFloor].position;
	  if( liftDoorState[refreshingFloor].state & DoorOpen )
	  {
		  position = 4-position;
	  }
	  liftDoors = doorPositions[position] | (1<<refreshingFloor);
  } 
  
  buttonRefreshCounter++;
  
  // Ansteuerung der einzelnen LED (Ruftastenanzeige, Etagenauswahlanzeige)
	if (displayCache > 0)
	{
		uint8_t floor = buttonRefreshCounter % 4;
		if( displayCache & (0x10<<floor))
		{
			buttonLiftLed = floor;
		}
		if( displayCache & (1<<floor))
		{
			buttonFloorLed = floor;	
		}
	}



  if ((displayCache & 0xF0) == 0) buttonLiftLed_on_tmp = Off;
  else buttonLiftLed_on_tmp = buttonLiftLed_on;
   
  if ((displayCache & 0x0F) == 0) buttonFloorLed_On_tmp = Off;
  else buttonFloorLed_On_tmp = buttonFloorLed_On;
  
  
  // Einzelzustaende werden auf die Ausgabeports gemeinsam ausgegeben
  liftPositionPort = (liftPosDisplay_On)? 0x3F : (Elevator.Position & 0x3f);
  floorDisplayPort = (liftDisplay7Seg_On)?  0x07: liftDisplay7Seg;
  liftPos     = liftPositionPort | (doorframe<<6) |(buttonLiftLed_on_tmp <<7);								// Ausgabe an PORTB
  liftDisplay = floorDisplayPort | (buttonLiftLed <<3) | (buttonFloorLed <<5) |(buttonFloorLed_On_tmp <<7); // Ausgabe an PORTA; Rufanzeigen Etage und Lift
  
  while(USART_rxBufferOut != USART_rxBufferIn)
  {
	  EnterAtomic();
	  char receivedData = USART_rxBuffer[USART_rxBufferOut++];
	  USART_rxBufferOut%=USART_RX_BUFFER_SIZE;
	  LeaveAtomic();	  
	  HandleMessage(receivedData);
  }
  
  if( terminalRefreshCounter++ == 0xFFF && EnableStatusUpdate )
  {
	  
	  Usart_PutChar(PacketType_LiftStatus); // message type
	  Usart_PutChar(6);					    // message length
	  Usart_PutChar(0xA5);					// synch
	  Usart_PutChar(0x5A);					// synch
	  Usart_PutChar(SystemState);			// SystemState
	  Usart_PutChar(OpenDoors);				// OpenDoors
  }
    
}



/*******************************************************************************
* Lesen des Tastenzustandes (Lifttasten und Etagentasten)
*******************************************************************************/
ButtonStateType ReadKeyEvent (ButtonType button)
{  
	
	ButtonStateType buttonState= ((buttons & button)? Pressed: Released);
	return buttonState;
}


/*******************************************************************************
* Lesen des Lifttuerenzustandes einer Etage
*******************************************************************************/
DoorStateType ReadDoorState (FloorType floor)
{
	return liftDoorState[floor].state;
}


/*******************************************************************************
* Setzen des Lifttuerenzustandes einer Etage
*******************************************************************************/
void SetDoorState (DoorStateType desiredState, FloorType floor)
{
	EnterAtomic();
	DoorStateType currentState = liftDoorState[floor].state;
	DoorStateType maskeDesiredState = desiredState&0x30; 
//	Usart_PutChar(maskeDesiredState);
//	Usart_PutChar(currentState);
//	Usart_PutChar(floor);
	if( (maskeDesiredState&currentState) == 0 )
	{	
		liftDoorState [floor].position = 4;		
    }
	liftDoorState [floor].state = (maskeDesiredState|1);  
	
	LeaveAtomic();
}


/*******************************************************************************
* Verschieben der Fahrgastzelle (Lift)
*******************************************************************************/

void MoveElevator(uint8_t pos, PositionChangeSignal signal)
{
	Elevator.Target = pos;
	Elevator.Direction = DirectionNone;
	Elevator.OnPositionChanged = signal;
	if( Elevator.Target > Elevator.Position )
	{
		Elevator.Direction = Up;
	}
	else if( Elevator.Target < Elevator.Position )
	{
		Elevator.Direction = Down;
	}
}





/*******************************************************************************
* Gibt den Zustand des Liftes bekannt
*******************************************************************************/
LiftStateType ReadElevatorState()
{
	return Elevator.Status;
}


/*******************************************************************************
* Setzen der floorDisplayPort im Lift (7-Segment-Anzeige)
*******************************************************************************/
void SetDisplay (FloorType displayValue)
{
	liftDisplay7Seg = displayValue;
}


/*******************************************************************************
* Setzen der Ruftastenanzeige pro Etage
*******************************************************************************/
void SetIndicatorFloorState (FloorType floor)
{
  
  if (floor <= 3){
    displayCache |= (1<<floor); //Setzen der Bits: 0-3
  }
}


/*******************************************************************************
* Setzen der Etagenauswahlanzeige im Lift
*******************************************************************************/
void SetIndicatorElevatorState (FloorType floor){
  
  if (floor <= 3){
    displayCache |= (1<<(floor + 4)); //Setzen der Bits: 4-7
  }
}


/*******************************************************************************
* Loeschen der Ruftastenanzeige pro Etage
*******************************************************************************/
void ClrIndicatorFloorState (FloorType floor)
{  
  if (floor <= 3){
    displayCache &= ~(1<<floor); //Loeschen der Bits: 0-3
  }
}


/**
* @brief Loeschen der Etagenauswahlanzeige im Lift
*
* @param floor Etage, für welche die LED ausgeschaltet werden soll!
*/
void ClrIndicatorElevatorState (FloorType floor)
{  
  if (floor <= 3)
  {
    displayCache &= ~(1<<(floor + 4)); //Loeschen der Bits: 4-7
  }
}

void RegisterTestHandler( TestHandlerCallback testHandler_ )
{
	_testHandler = testHandler_;
}

/******************************************************************
* setup the UART
*******************************************************************/
void Usart_Init(void)
{
	
	UBRRH = 0;
	UBRRL = 12;								// initialize baud rate = 38400
	UCSRC = 0x86;							//8 data bits, 1 stop bit, not parity; docu page 162	
	
	UCSRB = (1<<TXEN)|(1<<RXEN)|(1<<RXCIE);	// enabe tx, rx and rx interrupts
	
	sei();									// enable interrupts 
}


/******************************************************************
* send one character to the PC
*******************************************************************/
void Usart_PutChar( char ch)
{

	/* Wait for empty transmit buffer */
	
	/* Put data into buffer, sends the data */
	//while ( !( UCSRA & (1<<UDRE))  uint8_t tmp_sreg;  // temporaerer Speicher fuer das Statusregister
		
	UDR = ch;
	while ( !( UCSRA & (1<<UDRE)) ); 
	//while ( !( UCSRA & (1<<TXC)) ); //flush the buffer immediately!

}

ISR(INT0_vect)
{
	ButtonState |= 0x100;
	SendEvent(SignalSourceDoor, DoorEmergencyBreak, ButtonState, 0);
}



ISR(USART_RXC_vect)
{
	while(UCSRA&(1<<RXC))
	{
		
		if( ((USART_rxBufferIn + 1)%USART_RX_BUFFER_SIZE) != USART_rxBufferOut )
		{
			char receivedChar = UDR;
			USART_rxBuffer[USART_rxBufferIn++] = receivedChar;
			USART_rxBufferIn %= USART_RX_BUFFER_SIZE;
			continue;
		}
		UCSRB &= ~(1<<RXCIE); // clear the rx interrupt since we will get another interrupt as soon as we exit here! 
		return; // in case there is no space left, we should just simply return!
	}
}




ISR(TIMER1_COMPA_vect)
{
	static uint8_t ElevatorTick = 0;
	static uint8_t DoorTick = 0;
	
	ElevatorTick++;
	DoorTick ++;
	if( (ElevatorTick % Elevator.Speed) == 0 )
	{
		if( Elevator.Position != Elevator.Target)
		{
			Elevator.Position += Elevator.Direction;		
			
			if ( Elevator.OnPositionChanged != 0)
			{
				Elevator.OnPositionChanged(Elevator.Position, Elevator.Target);
			}
		}
		ElevatorTick = 0;
	}
	if ((DoorTick%DOOR_OPENCLOSE_SPEED) == 0)
	{
		DoorTick = 0;
		MakeDoorStates();
	}
}


uint8_t StartTimer(uint16_t ms)
{
	uint8_t i = 0;
	for( ; i < 8; i++ )
	{
		if( !(UsedTimers&(1<<i)) )
		{
			EnterAtomic();
			UsedTimers|= (1<<i);
			WaitingTimer[i] = ms;
			LeaveAtomic();
			return i;
		}
	}
	return 0xFF;
}

void StopTimer(uint8_t id)
{
	if( id < 8)
	{	
		EnterAtomic();
		UsedTimers &= ~(1<<id);
		WaitingTimer[id] = 0;
		LeaveAtomic();
	}
}


ISR(TIMER0_COMP_vect)
{

	for( int i = 0; i <8; i++ )
	{
		if( UsedTimers&(1<<i))
		{
			WaitingTimer[i]--;
			if( WaitingTimer[i]==0)
			{
				SendEvent(SignalSourceEnvironment, TimerEvent, 0, 0);
				UsedTimers &= ~(1<<i);
			}
		}
	}
}
