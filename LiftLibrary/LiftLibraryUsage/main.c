/*
 * LiftLibraryUsage.c
 *
 * Created: 20.11.2019 16:35:00
 * Author : rolfL
 */ 

#include "LiftLibrary.h"

void SysState_Initializing( Message* msg);
void SysState_AwaitRequest( Message* msg);
void SysState_AwaitTargetSelection(Message *msg);
void SysState_Moving(Message* msg);

void MotorState_DoorOpening(Message* msg);
void MotorState_DoorClosing(Message* msg);
void MotorState_Stopped(Message* msg);
void MotorState_Moving(Message* msg);


#define IS_ANY_RESERVATION_BUTTON_PRESSED(buttonState) (buttonState&0xF0)


typedef enum
{
	Message_MoveTo = 1, 	
	Message_PosChanged = 2,
}ApplicationMessages;

typedef struct  
{
	Fsm fsm;
	FloorType floor;
	uint8_t timer;
}MainController;

typedef struct  
{
	Fsm fsm;
	FloorType start;
	FloorType target;
}MotorController;

static MainController _systemControl = {
	.floor = Floor0,
	.timer = 0xFF,
	.fsm = {
		.Next = 0, .RxMask = SignalSourceEnvironment, .CurrentState = SysState_Initializing
	}
};


static MotorController _motorControl = 
{
	.start = Floor0,
	.target = Floor0,
	.fsm = {
		.CurrentState = MotorState_Stopped, .Next = 0, .RxMask = SignalSourceElevator|SignalSourceDoor|SignalSourceApp
	}
};

FloorType FindFloorReservation(uint8_t buttonState);
FloorType FindElevatorTarget(uint8_t buttonState);

int main(void)
{
	
	InitializePorts();   
    Usart_Init();
	
	RegisterFsm(&_motorControl.fsm);
	RegisterFsm(&_systemControl.fsm);
	InitializeStart();
	
	return 0;
}


void NotifyCalibrationDone(uint8_t currentPos, uint8_t targetPostion)
{
	FloorType floor = (FloorType)currentPos/16;
	SetDisplay(floor);
	if( ((currentPos %floor) == 0 ) && floor == Floor0 )
	{
		SendEvent(SignalSourceEnvironment, LiftCalibrated, currentPos, targetPostion);
	}
}
	

void SysState_Initializing( Message* msg)
{
	if( msg->Id == LiftStarted )
	{
		SetDisplay(Floor2);
		CalibrateElevatorPosition(NotifyCalibrationDone);
		return;
	}
	if( msg->Id == LiftCalibrated )
	{
		_systemControl.fsm.RxMask |= (SignalSourceDoor|SignalSourceEtageButton|SignalSourceLiftButton|SignalSourceApp);
		SetState(&_systemControl.fsm, SysState_AwaitRequest);
		SetDisplay(Floor0);
		EnableStatusUpdate = true;
		return;
	}
}

void SysState_AwaitRequest(Message *msg)
{	
	if( msg->Id == ButtonEvent && msg->MsgParamHigh == Pressed )
	{
		Usart_PutChar(0xE2);
		if (IS_ANY_RESERVATION_BUTTON_PRESSED(msg->MsgParamLow) )
		{
			Usart_PutChar(0xE3);
			if (IS_RESERVATION_BUTTON_PRESSED(msg->MsgParamLow, _systemControl.floor) )
			{
				Usart_PutChar(0xE4);
				SetState(&_systemControl.fsm, SysState_AwaitTargetSelection);
				SetDoorState(DoorOpen, _systemControl.floor);
				_systemControl.timer = StartTimer(10000);
			}
			else
			{
				Usart_PutChar(0xE5);
				SetState(&_systemControl.fsm, SysState_Moving );
				FloorType requestedFloor = FindFloorReservation(msg->MsgParamLow);
				ClrIndicatorFloorState(_systemControl.floor);
				SetIndicatorFloorState(requestedFloor);
				SendEvent(SignalSourceApp, Message_MoveTo, _systemControl.floor, requestedFloor);
			}
		}
	}
}


void SysState_AwaitTargetSelection(Message *msg)
{
	Usart_PutChar(0x41);
	Usart_PutChar(msg->Id);
	if( msg->Id == ButtonEvent && msg->MsgParamHigh == Pressed)
	{
		StopTimer(_systemControl.timer);
		FloorType target = _systemControl.floor;
		uint8_t buttonState = msg->MsgParamLow;
		if (IS_ANY_RESERVATION_BUTTON_PRESSED(buttonState)) // es ist ein Reservations-Button
		{
			target = FindFloorReservation(buttonState);
			SetIndicatorFloorState(target);
		}
		else
		{
			target = FindElevatorTarget(buttonState);
			SetIndicatorElevatorState(target);
		}
		if( target != _systemControl.floor)
		{
			Usart_PutChar(0x44);
			Usart_PutChar(buttonState);
			Usart_PutChar(target);
			Usart_PutChar(0x45);
			ClrIndicatorFloorState(_systemControl.floor);
			SetState(&_systemControl.fsm, SysState_Moving);
			SendEvent(SignalSourceApp, Message_MoveTo, _systemControl.floor, target);
			return;
		}
		
	}
	if(msg->Id == TimerEvent ||
	( msg->Id == ButtonEvent && msg->MsgParamHigh == Pressed))
	{
		SetDoorState(DoorClosed, _systemControl.floor);
		SetState(&_systemControl.fsm, SysState_AwaitRequest);
	}
}



void SysState_Moving(Message* msg)
{
	Usart_PutChar(0x31);
	Usart_PutChar(msg->Id);
	if( msg->Id == ButtonEvent && msg->MsgParamHigh == Pressed)
	{
		return;
	}
	if( msg->Id == ElevatorAtFloor)
	{
		SetState( &_systemControl.fsm, SysState_AwaitTargetSelection );
		_systemControl.timer = StartTimer(10000);
		_systemControl.floor = msg->MsgParamLow;
		ClrIndicatorElevatorState(_systemControl.floor);
	}
}

void MotorState_Moving(Message* msg)
{
	//Usart_PutChar(0x81);
	//Usart_PutChar(msg->Id);
	if( msg->Id == Message_PosChanged)
	{
		//Usart_PutChar(0x82);
		//Usart_PutChar(_motorControl.target);
		FloorType floor = msg->MsgParamLow/16; 
		if( msg->MsgParamLow%16 == 0)
		{
			SetDisplay(floor);
			if( floor == _motorControl.target )
			{
				SetState(&_motorControl.fsm, MotorState_DoorOpening );
				SetDoorState(DoorOpen, floor);
			}
		}
		return;
	}
}


void MotorState_DoorOpening(Message* msg)
{
	Usart_PutChar(0x51);
	if( msg->Id == LiftDoorEvent && msg->MsgParamLow == DoorOpen )
	{
		Usart_PutChar(0x52);
		SetState(&_motorControl.fsm, MotorState_Stopped);
		SendEvent(SignalSourceApp, ElevatorAtFloor, _motorControl.target, 0 );
	}
}

void OnElevatorPositionChanged(uint8_t currentPos, uint8_t targetPos)
{
	SendEvent(SignalSourceElevator, Message_PosChanged, currentPos, targetPos);
}

void MotorState_DoorClosing(Message* msg)
{
	Usart_PutChar(0x54);
	if( msg->Id == LiftDoorEvent && msg->MsgParamLow == DoorClosed)
	{
		Usart_PutChar(0x55);
		if ( msg->MsgParamHigh == _motorControl.target )
		{
			Usart_PutChar(0x57);
			SetState(&_motorControl.fsm, MotorState_Stopped);
		}
		else
		{
			Usart_PutChar(0x58);
			SetState(&_motorControl.fsm, MotorState_Moving);
			Usart_PutChar(_motorControl.target * POS_STEP_PER_FLOOR);
			MoveElevator(_motorControl.target * POS_STEP_PER_FLOOR, OnElevatorPositionChanged );
		}
	}
}

void MotorState_Stopped(Message* msg)
{
	Usart_PutChar(0xD1);
	if( msg->Id == Message_MoveTo )
	{
		_motorControl.start = msg->MsgParamLow;
		_motorControl.target = msg->MsgParamHigh;
		Usart_PutChar(0xD2);
		Usart_PutChar(_motorControl.start);
		SetState(&_motorControl.fsm, MotorState_DoorClosing);
		SetDoorState(DoorClosed, _motorControl.start);
	}
}

FloorType FindFloorReservation(uint8_t buttonState)
{
	uint8_t i = 4;
	for(;i<8;i++)
	{
		if( (1<<i)&buttonState )
		{
			return (FloorType)(i-4);
		}
	}
	return _systemControl.floor;
}

FloorType FindElevatorTarget(uint8_t buttonState)
{
	uint8_t i = 0;
	for(;i<4;i++)
	{
		if( (1<<i)&buttonState )
		{
			return (FloorType)i;
		}
	}
	return _systemControl.floor;
}

