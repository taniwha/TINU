export MODNAME		:= TINU
export KSPDIR		:= ${HOME}/ksp/KSP_linux
export MANAGED		:= ${KSPDIR}/KSP_Data/Managed
export GAMEDATA		:= ${KSPDIR}/GameData
export MODGAMEDATA	:= ${GAMEDATA}/${MODNAME}
export PLUGINDIR	:= ${MODGAMEDATA}/Plugins

TARGETS		:= TINU.dll

TINU_FILES := \
	assembly/AssemblyInfo.cs	\
	assembly/VersionReport.cs	\
	FlightCameraOverride.cs		\
	TINUFlightCamera.cs			\
	Toolbar.cs					\
	UI/ConfigWindow.cs			\
	UI/ScrollView.cs			\
	UI/Styles.cs				\
	UI/TextField.cs				\
	$e

RESGEN2		:= resgen2
CSC			:= csc
CSCFLAGS	:= -highentropyva- -noconfig -nostdlib+ -t:library -optimize -optimize -debug -warnaserror
GIT			:= git
TAR			:= tar
ZIP			:= zip

all: version ${TARGETS}

.PHONY: version
version:
	@../tools/git-version.sh

info:
	@echo "${MODNAME} Build Information"
	@echo "    resgen2:    ${RESGEN2}"
	@echo "    gmcs:       ${GMCS}"
	@echo "    gmcs flags: ${GMCSFLAGS}"
	@echo "    git:        ${GIT}"
	@echo "    tar:        ${TAR}"
	@echo "    zip:        ${ZIP}"
	@echo "    KSP Data:   ${KSPDIR}"

SYSTEM := \
	-lib:${MANAGED} \
	-r:${MANAGED}/mscorlib.dll \
	-r:${MANAGED}/System.dll \
	-r:${MANAGED}/System.Core.dll

KSP := \
	-r:Assembly-CSharp.dll \
	-r:Assembly-CSharp-firstpass.dll

UNITY := \
	-r:UnityEngine.dll \
	-r:UnityEngine.UI.dll \
	-r:UnityEngine.UIModule.dll \
	-r:UnityEngine.IMGUIModule.dll \
	-r:UnityEngine.TextRenderingModule.dll \
	-r:UnityEngine.AnimationModule.dll \
	-r:UnityEngine.CoreModule.dll \
	-r:UnityEngine.PhysicsModule.dll \
	-r:UnityEngine.InputLegacyModule.dll \
	$e

TINU.dll: ${TINU_FILES}
	${CSC} ${CSCFLAGS} ${SYSTEM} ${KSP} ${UNITY} -out:$@ $^

clean:
	rm -f ${TARGETS}* assembly/AssemblyInfo.cs ${MODNAME}.version

install: all
	mkdir -p ${PLUGINDIR}
	cp ${TARGETS} ${MODNAME}.version ${PLUGINDIR}

.PHONY: all clean install
