export MODNAME		:= TINU
export KSPDIR		:= ${HOME}/ksp/KSP_linux
export MANAGED		:= ${KSPDIR}/KSP_Data/Managed
export GAMEDATA		:= ${KSPDIR}/GameData
export MODGAMEDATA	:= ${GAMEDATA}/${MODNAME}
export PLUGINDIR	:= ${MODGAMEDATA}/Plugins

TARGETS		:= bin/TINU.dll

TINU_FILES := \
	assembly/AssemblyInfo.cs	\
	assembly/VersionReport.cs	\
	FlightCameraOverride.cs		\
	TINUFlightCamera.cs			\
	$e

RESGEN2		:= resgen2
GMCS		:= mcs
GMCSFLAGS	:= -optimize -warnaserror -debug
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

bin/TINU.dll: ${TINU_FILES}
	@mkdir -p bin
	${GMCS} ${GMCSFLAGS} -t:library -lib:${MANAGED} \
		-r:Assembly-CSharp,Assembly-CSharp-firstpass \
		-r:UnityEngine \
		-r:UnityEngine.UI \
		-r:UnityEngine.UIModule \
		-r:UnityEngine.IMGUIModule \
		-r:UnityEngine.TextRenderingModule \
		-r:UnityEngine.AnimationModule \
		-r:UnityEngine.CoreModule \
		-r:UnityEngine.PhysicsModule \
		-r:UnityEngine.InputLegacyModule \
		-out:$@ $^

clean:
	rm -f ${TARGETS}* assembly/AssemblyInfo.cs bin/${MODNAME}.version
	test -d bin && rmdir bin || true

install: all
	mkdir -p ${PLUGINDIR}
	cp ${TARGETS} bin/${MODNAME}.version ${PLUGINDIR}

.PHONY: all clean install
