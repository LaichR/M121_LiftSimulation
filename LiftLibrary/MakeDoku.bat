
IF NOT EXIST %GIT_ROOT%\M121_LiftSimulation\LiftLibrary\Doku mkdir  %GIT_ROOT%\M121_LiftSimulation\LiftLibrary\Doku
doxygen %GIT_ROOT%\M121_LiftSimulation\LiftLibrary\LiftLibraryDoku.xml
%GIT_ROOT%\M121_LiftSimulation\LiftLibrary\Doku\latex\make.bat