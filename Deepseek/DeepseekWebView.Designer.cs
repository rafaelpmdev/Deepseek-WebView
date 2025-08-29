using Microsoft.Web.WebView2.WinForms;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

namespace Deepseek
{
    public partial class DeepseekWebView
    {
        private WebView2 webView;
        private bool verificationPassed = false; //Controle de verificação
        private DateTime loadStartTime; // Tempo de início do carregamento
        private bool disposed = false; // Flag para evitar disposição dupla

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DeepseekWebView));
            SuspendLayout();

            Icon = LoadIconFromResources();

            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.ClientSize = new System.Drawing.Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "DeepSeek Chat";
            this.ResumeLayout(false);

            InitializeWebViewComponent();
        }

        /// <summary>
        /// Solução escolhida para evitar perda nas imagens do ícone e em interações com o sistema.
        /// </summary>
        private Icon LoadIconFromResources()
        {
            try
            {
                // Carrega o ícone embutido
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Deepseek.deepseek_logo_icon.ico"))
                {
                    return new Icon(stream);
                }
            }
            catch
            {
                // Fallback: ícone padrão se houver erro
                return SystemIcons.Application;
            }
        }

        /// <summary>
        /// Inicializa o componente WebView2, que irá redirecionar para o site do deepseek, 
        /// configurando propriedades, adicionando eventos
        /// e gerencindo cookies de forma adequada.
        /// </summary>
        private async void InitializeWebViewComponent()
        {
            //Configurações iniciais do Navegador
            webView = new WebView2
            {
                Dock = DockStyle.Fill,
                CreationProperties = new CoreWebView2CreationProperties
                {
                    UserDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\DeepSeekBrowser",
                    BrowserExecutableFolder = null
                }
            };

            // Adiciona o WebView2 ao form
            this.Controls.Add(webView);

            //Parte gerencial e de eventos do WebView2
            try
            {
                // Inicialização assíncrona preparando o ambiente WebView2
                await webView.EnsureCoreWebView2Async(null);

                // Somente necessário se precisar de um user-agent customizado
                // webView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

                // Desabilita menus de contexto padrão
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.IsZoomControlEnabled = true;

                // Eventos para controle de navegação
                webView.CoreWebView2.NavigationStarting += NavigationStarting; 
                webView.CoreWebView2.NavigationCompleted += NavigationCompleted;
                webView.CoreWebView2.DOMContentLoaded += DOMContentLoaded; 

                // Solução alternativa para cookies
                webView.CoreWebView2.CookieManager.DeleteAllCookies();

                // Navegação inicial
                loadStartTime = DateTime.Now;
                webView.CoreWebView2.Navigate("https://chat.deepseek.com/");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro na inicialização: {ex.Message}");
                MessageBox.Show($"Erro ao inicializar o navegador: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Manipula o evento de início de navegação no WebView2, registrando a URL de destino
        /// e controlando a permissão para prosseguir com a navegação.
        /// Este método é executado antes de qualquer requisição de navegação ser processada.
        /// </summary>
        /// <param name="sender">A origem do evento (WebView2)</param>
        /// <param name="eventArgs">Argumentos do evento contendo informações sobre o carregamento do DOM</param>
        private void NavigationStarting(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs eventArgs)
        {
            Debug.WriteLine($"Navegando para: {eventArgs.Uri}");

            // Permite todos os redirecionamentos
            eventArgs.Cancel = false;
        }

        /// <summary>
        /// Manipula o evento de conclusão de navegação no WebView2, verificando o sucesso
        /// da operação e gerenciando tempo limite de verificação de segurança.
        /// Executa ações corretivas quando o tempo de verificação excede o limite máximo.
        /// </summary>
        /// <param name="sender">A origem do evento (WebView2)</param>
        /// <param name="eventArgs">Argumentos do evento contendo informações sobre o carregamento do DOM</param>
        private async void NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs eventArgs)
        {
            Debug.WriteLine($"Navegação completada: {eventArgs.IsSuccess}");

            if (!eventArgs.IsSuccess)
            {
                Debug.WriteLine($"Código de erro: {eventArgs.WebErrorStatus}");
                return;
            }

            // Verifica se estamos no loop de verificação
            if ((DateTime.Now - loadStartTime).TotalSeconds > 30 && !verificationPassed)
            {
                await webView.CoreWebView2.ExecuteScriptAsync("document.body.style.backgroundColor='red';");
                Debug.WriteLine("Tempo de verificação excedido - recarregando");
                webView.CoreWebView2.Reload();
            }
        }

        /// <summary>
        /// Manipula o evento de carregamento do DOM da página, verificando automaticamente
        /// a conclusão de verificações de segurança como captchas ou desafios de humanidade.
        /// Este método é executado quando o conteúdo DOM está pronto, antes do carregamento
        /// completo de recursos como imagens e stylesheets.
        /// </summary>
        /// <param name="sender">A origem do evento (WebView2)</param>
        /// <param name="eventArgs">Argumentos do evento contendo informações sobre o carregamento do DOM</param>
        private async void DOMContentLoaded(object sender, Microsoft.Web.WebView2.Core.CoreWebView2DOMContentLoadedEventArgs eventArgs)
        {
            try
            {
                string content = await webView.CoreWebView2.ExecuteScriptAsync(
                    "document.body.innerText.includes('Verificando se você é humano') ? 'verifying' : 'verified'");

                if (content.Contains("verified"))
                {
                    verificationPassed = true;
                    Debug.WriteLine("Verificação concluída com sucesso");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro no DOMContentLoaded: {ex.Message}");
            }
        }

        /// <summary>
        /// Mecanismo para liberar recursos não gerenciados (como handles, conexões, memória) de forma controlada.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Libera recursos gerenciados
                    if (webView != null)
                    {
                        // Desregistra eventos
                        if (webView.CoreWebView2 != null)
                        {
                            webView.CoreWebView2.NavigationStarting -= NavigationStarting;
                            webView.CoreWebView2.NavigationCompleted -= NavigationCompleted;
                        }

                        // Para navegação e libera recursos
                        webView.CoreWebView2?.Stop();
                        webView.Dispose();
                        webView = null;
                    }
                }

                // Libera recursos não gerenciados aqui (se houver)
                disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}