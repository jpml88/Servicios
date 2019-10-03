using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Configuration;
using System.Net;
using Servicio.Sistemas.Sistema_Integral.BusinessLayer.ValueObjects;
using Servicio.Sistemas.Servicios.BusinessLayer.BusinessObjects;

namespace Servicio_General
{
    public partial class Servicio_General : ServiceBase
    {
        public IList<Thread> _Hilos_En_Proceso = new List<Thread>();

        public Servicio_General()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                Thread _th1 = new Thread(new ParameterizedThreadStart(Cargar_Servicios));
                _th1.Name = "TKFCargar_Servicios";
                _th1.Start(null);
                _Hilos_En_Proceso.Add(_th1);
                EventLog.WriteEntry("Inicio Servicio General");
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Error: " + ex.Message);
            }
        }

        private void Cargar_Servicios(object pVariable_Sin_Uso)
        {
            Thread _thread_actual = Thread.CurrentThread;
            BO_Servicio _servicio = new BO_Servicio();
            RS_Servicios _respuesta = new RS_Servicios();
            try
            {
                while (_thread_actual.ThreadState == System.Threading.ThreadState.Running)
                {
                    try
                    {
                        IList<Thread> _threads_a_borrar = new List<Thread>();
                        foreach (Thread _thread in _Hilos_En_Proceso)
                        {
                            if (_thread.ThreadState != System.Threading.ThreadState.Running &&
                               _thread.ThreadState != System.Threading.ThreadState.WaitSleepJoin &&
                               _thread.ThreadState != System.Threading.ThreadState.Background)
                            {
                                _threads_a_borrar.Add(_thread);
                            }
                        }
                        foreach (Thread _thread_a_borrar in _threads_a_borrar)
                        {
                            _Hilos_En_Proceso.Remove(_thread_a_borrar);
                        }
                        _threads_a_borrar.Clear();
                        _respuesta = _servicio.Obtener_Servicios_Activos();
                        if (_respuesta.Resultado == Resultado.Error)
                        {
                            throw new Exception(_respuesta.Mensaje);
                        }
                        if (_respuesta.Servicios == null)
                        {
                            throw new Exception("Array 'Servicios' nulo.");
                        }
                        Thread _th_aux = null;
                        foreach (OServicio _objeto in _respuesta.Servicios)
                        {
                            if (_objeto.Id_Servicio_Estado == 1)
                            {
                                Boolean _encontro = false;
                                String _nombre = "TKFServicios" + _objeto.Id_Servicio.ToString("0000");
                                foreach (Thread _hilo in _Hilos_En_Proceso)
                                {
                                    if (_hilo.Name == _nombre)
                                    {
                                        _encontro = true;
                                        break;
                                    }
                                }
                                if (_encontro == false)
                                {
                                    if (_objeto.Id_Servicio_Tipo == 1)
                                    {
                                        _th_aux = new Thread(new ParameterizedThreadStart(Control_Constante));
                                    }
                                    else
                                    {
                                        _th_aux = new Thread(new ParameterizedThreadStart(Control_Programable));
                                    }

                                    _th_aux.Name = _nombre;
                                    _th_aux.Start(_objeto.Id_Servicio);
                                    _Hilos_En_Proceso.Add(_th_aux);
                                }
                            }
                        }
                        Thread.Sleep(30000);
                    }
                    catch (Exception ex)
                    {
                        EventLog.WriteEntry("Error Grave. El Thread Cargar_Servicios tuvo un error en el while. Detalle:" + ex.Message);
                        Thread.Sleep(30000);
                    }
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Error Grave. El Thread Cargar_Servicios dejo de operar. Detalle:" + ex.Message);
            }

        }

        private void Control_Constante(object pId_Servicio)
        {
            Thread _thread_actual = Thread.CurrentThread;
            Int32 _id_servicio = (Int32)pId_Servicio;
            try
            {
                Int16 _contador_errores = 0;
                String _ultimo_error = "";

                int _tiempo_proceso = 60000;

                BO_Servicio _metodos = new BO_Servicio();
                RS_Servicio _respuesta = new RS_Servicio();

                while (_thread_actual.ThreadState == System.Threading.ThreadState.Running)
                {
                    try
                    {
                        _respuesta = _metodos.Procesar_Servicio(_id_servicio, _contador_errores, _ultimo_error);
                        if (_respuesta.Resultado == Resultado.Error)
                        {
                            throw new Exception(_respuesta.Mensaje);
                        }

                        _contador_errores = 0;
                        _ultimo_error = "";

                        if (_respuesta.Servicio.Id_Servicio_Estado != 1)
                        {
                            break;
                        }

                        if (_respuesta.Servicio.Id_Servicio_Tipo == 1)
                        {
                            RS_Servicio_Constante _servicio_constante = _metodos.Obtener_Servicio_Constante(_id_servicio);
                            _tiempo_proceso = _servicio_constante.Servicio.Control_Cada_X_Segundos * 1000;
                        }

                        Thread.Sleep(_tiempo_proceso);
                    }
                    catch (Exception ex)
                    {
                        if (_contador_errores == 10)
                        {
                            _contador_errores = 0;
                            _ultimo_error = "";
                        }
                        _contador_errores++;
                        _ultimo_error = ex.Message;
                        Thread.Sleep(5000);
                        EventLog.WriteEntry("Error en control de Servicio N° " + _id_servicio.ToString("0000") + ". Mensaje:" + _ultimo_error);
                    }
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Error Grave. El thread Control_Constante para el servicio N° '" + _id_servicio + "' dejo de operar. Detalle:" + ex.Message);
            }
        }

        private void Control_Programable(object pId_Servicio)
        {
            Thread _thread_actual = Thread.CurrentThread;
            Int32 _id_servicio = (Int32)pId_Servicio;
            
            try
            {
                Int16 _contador_errores = 0;
                String _ultimo_error = "";

                int _tiempo_proceso = 60000;

                WS_Servicio.WS_Servicio _servicio = new WS_Servicio.WS_Servicio();
                WS_Servicio.RS_Servicio _respuesta = new WS_Servicio.RS_Servicio();

                while (_thread_actual.ThreadState == System.Threading.ThreadState.Running)
                {
                    try
                    {
                        _respuesta = _servicio.Procesar_Servicio(_id_servicio, _contador_errores, _ultimo_error);
                        if (_respuesta.Resultado == WS_Servicio.Resultado.Error)
                        {
                            throw new Exception(_respuesta.Mensaje);
                        }

                        _contador_errores = 0;
                        _ultimo_error = "";

                        if (_respuesta.Servicio.Id_Servicio_Estado != 1)
                        {
                            break;
                        }

                        if (_respuesta.Servicio.Id_Servicio_Tipo == 1)
                        {
                            WS_Servicio.RS_Servicio_Constante _servicio_constante = _servicio.Obtener_Servicio_Constante(_id_servicio);
                            _tiempo_proceso = _servicio_constante.Servicio.Control_Cada_X_Segundos * 1000;
                        }

                        Thread.Sleep(_tiempo_proceso);
                    }
                    catch (Exception ex)
                    {
                        if (_contador_errores == 10)
                        {
                            _contador_errores = 0;
                            _ultimo_error = "";
                        }
                        _contador_errores++;
                        _ultimo_error = ex.Message;
                        Thread.Sleep(5000);
                        EventLog.WriteEntry("Error en control de Servicio N° " + _id_servicio.ToString("0000") + ". Mensaje:" + _ultimo_error);
                    }
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Error Grave. El thread Control_Constante para el servicio N° '" + _id_servicio + "' dejo de operar. Detalle:" + ex.Message);
            }
        }

        private void Eliminar_Hilo_De_Lista(String pNombre_Hilo)
        {
            try
            {
                Thread _hilo_a_eliminar = null;

                foreach (Thread _hilo in _Hilos_En_Proceso)
                {
                    if (_hilo.Name == pNombre_Hilo)
                    {
                        _hilo_a_eliminar = _hilo;
                        break;
                    }
                }

                if (_hilo_a_eliminar != null)
                {
                    _Hilos_En_Proceso.Remove(_hilo_a_eliminar);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        protected override void OnStop()
        {
            foreach (Thread _thread in _Hilos_En_Proceso)
            {
                if (_thread.ThreadState == System.Threading.ThreadState.Running)
                {
                    _thread.Interrupt();
                    _thread.Abort();
                }
            }

            EventLog.WriteEntry("Cierre Servicio General");
        }
    }

}
