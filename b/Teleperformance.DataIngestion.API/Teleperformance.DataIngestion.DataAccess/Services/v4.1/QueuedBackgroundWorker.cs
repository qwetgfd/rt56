using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Services.v4._1;
using Microsoft.Extensions.Hosting;

namespace Teleperformance.DataIngestion.DataAccess.Services.v4._1
{





    public class QueuedBackgroundWorker : BackgroundService
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly ILogger<QueuedBackgroundWorker> _logger;
        private readonly IServiceProvider _serviceProvider;

        public QueuedBackgroundWorker(
            IBackgroundTaskQueue taskQueue,
            IServiceProvider serviceProvider,
            ILogger<QueuedBackgroundWorker> logger)
        {
            _taskQueue = taskQueue;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                try
                {
                    await workItem(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing background work item.");
                }
            }
        }
    }


}